"""
Context-Box RAG — File-Type-Aware Split Strategies

Each file type gets an optimized chunking strategy:

  TEXT / MARKDOWN     → Sentence-boundary splitter (paragraph → sentence → word)
  CODE (all langs)    → Sliding-window over logical blocks (function/class boundaries)
  JSON / YAML         → Top-level key grouping with optional recursive flattening
  CSV                 → Row-batch splitter (N rows per chunk with header preserved)
  HTML / XML          → Tag-section splitter (heading or section tags as delimiters)
  SQL                 → Statement splitter (semi-colon or DDL keyword boundaries)
  LOG                 → Fixed-line-count window (50 lines, 10-line overlap)
  PDF / DOCX / XLSX   → Generic recursive character splitter (binary extraction fallback)
"""

import re
import json
import csv
import io
import logging
from typing import List, Dict, Any, Optional
from dataclasses import dataclass, field

logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# Data model for a chunk (enriched with split metadata)
# ---------------------------------------------------------------------------

@dataclass
class Chunk:
    text: str
    index: int                           # position in the chunk sequence
    strategy: str                        # which strategy was applied
    metadata: Dict[str, Any] = field(default_factory=dict)


# ---------------------------------------------------------------------------
# Base splitter interface
# ---------------------------------------------------------------------------

class BaseSplitter:
    """Abstract base — all splitters must implement split()."""

    name: str = "base"

    def split(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        raise NotImplementedError


# ---------------------------------------------------------------------------
# 1. Recursive Character Splitter  (generic fallback)
# ---------------------------------------------------------------------------

class RecursiveCharacterSplitter(BaseSplitter):
    """
    Splits text trying paragraph → sentence → word boundaries before hard-cutting.
    Overlapping windows prevent context loss at chunk edges.

    Default: 1 000-char chunks with 200-char overlap.
    """

    name = "recursive_character"

    SEPARATORS = ["\n\n", "\n", ". ", "? ", "! ", "; ", ", ", " ", ""]

    def __init__(self, chunk_size: int = 1000, chunk_overlap: int = 200):
        self.chunk_size = chunk_size
        self.chunk_overlap = chunk_overlap

    def _split_recursive(self, text: str, separators: List[str]) -> List[str]:
        if not text:
            return []

        sep = separators[0]
        next_seps = separators[1:]

        if sep == "":
            # Hard cut as last resort
            pieces = [text[i:i + self.chunk_size] for i in range(0, len(text), self.chunk_size)]
            return [p for p in pieces if p.strip()]

        parts = text.split(sep)
        results: List[str] = []
        current = ""

        for part in parts:
            candidate = current + (sep if current else "") + part
            if len(candidate) <= self.chunk_size:
                current = candidate
            else:
                if current:
                    results.append(current)
                # If a single part is too long, recurse with next separator
                if len(part) > self.chunk_size and next_seps:
                    results.extend(self._split_recursive(part, next_seps))
                else:
                    current = part

        if current:
            results.append(current)

        return results

    def _apply_overlap(self, pieces: List[str]) -> List[str]:
        """Merge pieces into overlapping windows of up to chunk_size."""
        if not pieces:
            return []

        chunks: List[str] = []
        window = ""

        for piece in pieces:
            candidate = (window + "\n\n" + piece).strip() if window else piece
            if len(candidate) <= self.chunk_size:
                window = candidate
            else:
                if window:
                    chunks.append(window)
                # Keep last `chunk_overlap` chars as prefix for next window
                overlap_start = max(0, len(window) - self.chunk_overlap)
                window = window[overlap_start:] + "\n\n" + piece
                window = window.strip()

        if window:
            chunks.append(window)

        return chunks

    def split(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        pieces = self._split_recursive(text, self.SEPARATORS)
        windowed = self._apply_overlap(pieces)
        return [
            Chunk(text=c.strip(), index=i, strategy=self.name, metadata=metadata or {})
            for i, c in enumerate(windowed)
            if c.strip()
        ]


# ---------------------------------------------------------------------------
# 2. Sentence-Boundary Splitter  (TXT, MD, RST, plain prose)
# ---------------------------------------------------------------------------

class SentenceBoundarySplitter(BaseSplitter):
    """
    Aggregates sentences into chunks that respect paragraph boundaries.

    Strategy:
      - Split text at paragraph breaks (blank lines).
      - Within each paragraph, split by sentence-ending punctuation.
      - Accumulate sentences until chunk_size is reached, then emit.
      - Overlap = last `overlap_sentences` sentences carried into next chunk.
    """

    name = "sentence_boundary"

    _SENT_RE = re.compile(r'(?<=[.!?])\s+')

    def __init__(
        self,
        chunk_size: int = 800,
        chunk_overlap: int = 150,
        overlap_sentences: int = 2,
    ):
        self.chunk_size = chunk_size
        self.chunk_overlap = chunk_overlap
        self.overlap_sentences = overlap_sentences

    def _sentences(self, paragraph: str) -> List[str]:
        raw = self._SENT_RE.split(paragraph.strip())
        return [s.strip() for s in raw if s.strip()]

    def split(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        paragraphs = re.split(r'\n\s*\n', text)
        all_sentences: List[str] = []

        for para in paragraphs:
            sents = self._sentences(para)
            all_sentences.extend(sents)
            all_sentences.append("")  # paragraph boundary marker

        chunks: List[str] = []
        current_sents: List[str] = []
        current_len = 0

        for sent in all_sentences:
            if sent == "":
                # paragraph boundary — flush if large enough
                if current_len >= self.chunk_size // 2:
                    chunks.append(" ".join(current_sents))
                    current_sents = current_sents[-self.overlap_sentences:]
                    current_len = sum(len(s) for s in current_sents)
                continue

            candidate_len = current_len + len(sent) + 1
            if candidate_len > self.chunk_size and current_sents:
                chunks.append(" ".join(current_sents))
                current_sents = current_sents[-self.overlap_sentences:]
                current_len = sum(len(s) for s in current_sents)

            current_sents.append(sent)
            current_len += len(sent) + 1

        if current_sents:
            chunks.append(" ".join(current_sents))

        return [
            Chunk(text=c.strip(), index=i, strategy=self.name, metadata=metadata or {})
            for i, c in enumerate(chunks)
            if c.strip()
        ]


# ---------------------------------------------------------------------------
# 3. Code Sliding-Window Splitter  (Python, JS/TS, Go, Rust, Java, C/C++, etc.)
# ---------------------------------------------------------------------------

class CodeSlidingWindowSplitter(BaseSplitter):
    """
    Splits code files using language-aware block boundaries, then applies
    a sliding window with overlap to keep context across chunk edges.

    Block delimiters per language family:
      - Python  : def / class / async def
      - JS/TS   : function / class / const <name> = / export
      - Go      : func / type / var / const block
      - Rust    : fn / impl / struct / enum / trait / mod
      - Java/Kt : class / interface / fun / void / public
      - C/C++   : function-like lines (contains '{' )
      - SQL     : handled by SqlStatementSplitter → not routed here
      - Generic : falls back to sliding window over raw lines

    Overlap = `overlap_lines` lines shared between consecutive chunks.
    """

    name = "code_sliding_window"

    # Regex patterns for block-start detection per language family
    _BLOCK_PATTERNS: Dict[str, re.Pattern] = {
        "python": re.compile(r'^(async\s+)?def\s+\w+|^class\s+\w+', re.MULTILINE),
        "js":     re.compile(r'^(export\s+)?(async\s+)?function\s+\w+|^(const|let|var)\s+\w+\s*=|^class\s+\w+', re.MULTILINE),
        "go":     re.compile(r'^func\s+|^type\s+\w+\s+|^var\s+|^const\s+', re.MULTILINE),
        "rust":   re.compile(r'^(pub\s+)?(async\s+)?fn\s+|^(pub\s+)?impl\s+|^(pub\s+)?struct\s+|^(pub\s+)?enum\s+|^mod\s+', re.MULTILINE),
        "java":   re.compile(r'^\s*(public|private|protected|static).*\(.*\)\s*\{|^(public|private|protected)?\s*(class|interface|enum)\s+\w+', re.MULTILINE),
        "c":      re.compile(r'^\w[\w\s\*]+\(.*\)\s*\{', re.MULTILINE),
    }

    _EXT_TO_FAMILY = {
        ".py": "python",
        ".js": "js", ".jsx": "js", ".ts": "js", ".tsx": "js", ".mjs": "js",
        ".go": "go",
        ".rs": "rust",
        ".java": "java", ".kt": "java",
        ".c": "c", ".cpp": "c", ".h": "c", ".hpp": "c",
    }

    def __init__(
        self,
        chunk_size_lines: int = 80,
        overlap_lines: int = 15,
        file_extension: str = "",
    ):
        self.chunk_size_lines = chunk_size_lines
        self.overlap_lines = overlap_lines
        self.family = self._EXT_TO_FAMILY.get(file_extension.lower(), "generic")

    def _split_at_block_boundaries(self, lines: List[str]) -> List[List[str]]:
        """Find block start indices and split lines accordingly."""
        full_text = "\n".join(lines)
        pattern = self._BLOCK_PATTERNS.get(self.family)
        if not pattern:
            return []

        starts = [m.start() for m in pattern.finditer(full_text)]
        if not starts:
            return []

        # Convert char positions to line indices
        line_starts = []
        pos = 0
        line_idx = 0
        char_to_line = {}
        for ln, line in enumerate(lines):
            for _ in range(len(line) + 1):  # +1 for newline
                char_to_line[pos] = ln
                pos += 1

        for start_char in starts:
            ln = char_to_line.get(start_char, 0)
            if not line_starts or ln != line_starts[-1]:
                line_starts.append(ln)

        # Build blocks from boundary to boundary
        blocks = []
        for i, start in enumerate(line_starts):
            end = line_starts[i + 1] if i + 1 < len(line_starts) else len(lines)
            blocks.append(lines[start:end])

        return blocks

    def _sliding_window(self, lines: List[str]) -> List[List[str]]:
        """Fallback: pure sliding window over lines."""
        step = max(1, self.chunk_size_lines - self.overlap_lines)
        windows = []
        for start in range(0, len(lines), step):
            end = min(start + self.chunk_size_lines, len(lines))
            windows.append(lines[start:end])
            if end == len(lines):
                break
        return windows

    def split(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        lines = text.splitlines()
        if not lines:
            return []

        blocks = self._split_at_block_boundaries(lines)

        # Merge very small blocks until each chunk reaches chunk_size_lines
        merged: List[List[str]] = []
        buffer: List[str] = []

        for block in blocks:
            if len(buffer) + len(block) <= self.chunk_size_lines:
                buffer.extend(block)
            else:
                if buffer:
                    merged.append(buffer)
                # If a single block is oversized, apply sliding window to it
                if len(block) > self.chunk_size_lines:
                    merged.extend(self._sliding_window(block))
                    buffer = block[-self.overlap_lines:]  # carry overlap
                else:
                    buffer = block

        if buffer:
            merged.append(buffer)

        # If no blocks were detected, fall back to sliding window over all lines
        if not merged:
            merged = self._sliding_window(lines)

        chunks = []
        for i, block_lines in enumerate(merged):
            chunk_text = "\n".join(block_lines).strip()
            if chunk_text:
                chunks.append(
                    Chunk(
                        text=chunk_text,
                        index=i,
                        strategy=self.name,
                        metadata={**(metadata or {}), "language_family": self.family},
                    )
                )

        return chunks


# ---------------------------------------------------------------------------
# 4. JSON / YAML Key-Group Splitter
# ---------------------------------------------------------------------------

class JsonKeyGroupSplitter(BaseSplitter):
    """
    Splits JSON/YAML by grouping top-level keys into chunks.

    Strategy:
      - Parse JSON; for each top-level key, serialize its subtree.
      - Accumulate keys until chunk_size is exceeded, then emit.
      - For YAML: convert to text and apply sentence-boundary fallback
        (full YAML parsing requires PyYAML which may not be installed).
    """

    name = "json_key_group"

    def __init__(self, chunk_size: int = 1200, file_extension: str = ".json"):
        self.chunk_size = chunk_size
        self.ext = file_extension.lower()

    def split(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        if self.ext in (".yaml", ".yml"):
            # No PyYAML dependency guaranteed; sentence-boundary fallback
            return SentenceBoundarySplitter(chunk_size=self.chunk_size).split(text, metadata)

        try:
            data = json.loads(text)
        except (json.JSONDecodeError, ValueError):
            logger.warning("json_key_group: failed to parse JSON; falling back to recursive splitter")
            return RecursiveCharacterSplitter().split(text, metadata)

        if not isinstance(data, dict):
            # JSON array or primitive — serialize each element
            items = data if isinstance(data, list) else [data]
            chunks: List[Chunk] = []
            buffer = ""
            idx = 0
            for item in items:
                serialized = json.dumps(item, ensure_ascii=False, indent=2)
                candidate = buffer + (",\n" if buffer else "") + serialized
                if len(candidate) > self.chunk_size and buffer:
                    chunks.append(Chunk(text=buffer, index=idx, strategy=self.name, metadata=metadata or {}))
                    idx += 1
                    buffer = serialized
                else:
                    buffer = candidate
            if buffer:
                chunks.append(Chunk(text=buffer, index=idx, strategy=self.name, metadata=metadata or {}))
            return chunks

        # Top-level dict: group by key
        chunks = []
        buffer_keys: Dict[str, Any] = {}
        buffer_len = 0
        idx = 0

        for key, value in data.items():
            entry = {key: value}
            entry_str = json.dumps(entry, ensure_ascii=False, indent=2)
            entry_len = len(entry_str)

            if buffer_len + entry_len > self.chunk_size and buffer_keys:
                chunk_text = json.dumps(buffer_keys, ensure_ascii=False, indent=2)
                chunks.append(Chunk(text=chunk_text, index=idx, strategy=self.name, metadata=metadata or {}))
                idx += 1
                buffer_keys = {}
                buffer_len = 0

            buffer_keys[key] = value
            buffer_len += entry_len

        if buffer_keys:
            chunk_text = json.dumps(buffer_keys, ensure_ascii=False, indent=2)
            chunks.append(Chunk(text=chunk_text, index=idx, strategy=self.name, metadata=metadata or {}))

        return chunks


# ---------------------------------------------------------------------------
# 5. CSV Row-Batch Splitter
# ---------------------------------------------------------------------------

class CsvRowBatchSplitter(BaseSplitter):
    """
    Splits CSV into fixed-size row batches, always preserving the header row
    at the top of each chunk.

    Strategy:
      - Parse CSV rows.
      - Emit chunks of `rows_per_chunk` data rows.
      - Each chunk starts with the header, so it is self-contained.
    """

    name = "csv_row_batch"

    def __init__(self, rows_per_chunk: int = 50):
        self.rows_per_chunk = rows_per_chunk

    def split(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        reader = csv.reader(io.StringIO(text))
        rows = list(reader)

        if not rows:
            return []

        header = rows[0]
        data_rows = rows[1:]

        if not data_rows:
            # Header-only file
            return [Chunk(
                text=",".join(header),
                index=0,
                strategy=self.name,
                metadata={**(metadata or {}), "rows": 0},
            )]

        header_str = ",".join(header)
        chunks: List[Chunk] = []

        for i in range(0, len(data_rows), self.rows_per_chunk):
            batch = data_rows[i: i + self.rows_per_chunk]
            body = "\n".join(",".join(row) for row in batch)
            chunk_text = header_str + "\n" + body
            chunks.append(Chunk(
                text=chunk_text,
                index=len(chunks),
                strategy=self.name,
                metadata={
                    **(metadata or {}),
                    "row_start": i + 1,
                    "row_end": i + len(batch),
                    "total_rows": len(data_rows),
                },
            ))

        return chunks


# ---------------------------------------------------------------------------
# 6. HTML / XML Tag-Section Splitter
# ---------------------------------------------------------------------------

class HtmlTagSectionSplitter(BaseSplitter):
    """
    Splits HTML/XML by structural sections.

    HTML strategy:
      - Split at heading tags (h1–h4) and <section>, <article>, <main>, <div class="..."> boundaries.
      - Strip tags for cleaner embedding.
      - Apply character-size guard after sectioning.

    XML strategy:
      - Split at top-level element boundaries.
      - Content is kept as-is (tags provide structure context).
    """

    name = "html_tag_section"

    _HEADING_RE = re.compile(
        r'(<(?:h[1-4]|section|article|main|header|footer|nav)\b[^>]*>)',
        re.IGNORECASE,
    )
    _TAG_STRIP_RE = re.compile(r'<[^>]+>')
    _WHITESPACE_RE = re.compile(r'\s+')

    def __init__(self, chunk_size: int = 1000, chunk_overlap: int = 150, file_extension: str = ".html"):
        self.chunk_size = chunk_size
        self.chunk_overlap = chunk_overlap
        self.ext = file_extension.lower()

    def _strip_tags(self, html: str) -> str:
        text = self._TAG_STRIP_RE.sub(' ', html)
        return self._WHITESPACE_RE.sub(' ', text).strip()

    def _split_xml(self, text: str, metadata: Optional[Dict[str, Any]]) -> List[Chunk]:
        """Split XML at top-level element boundaries using regex."""
        # Find top-level elements naively (not full XML parse)
        top_elem_re = re.compile(r'(<(\w+)[^>]*>.*?</\2>)', re.DOTALL)
        matches = top_elem_re.findall(text)
        if not matches:
            return RecursiveCharacterSplitter(self.chunk_size, self.chunk_overlap).split(text, metadata)

        chunks: List[Chunk] = []
        buffer = ""
        for m in matches:
            element = m[0]
            if len(buffer) + len(element) > self.chunk_size and buffer:
                chunks.append(Chunk(text=buffer.strip(), index=len(chunks), strategy=self.name, metadata=metadata or {}))
                buffer = element
            else:
                buffer += "\n" + element

        if buffer.strip():
            chunks.append(Chunk(text=buffer.strip(), index=len(chunks), strategy=self.name, metadata=metadata or {}))

        return chunks

    def split(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        if self.ext == ".xml":
            return self._split_xml(text, metadata)

        # HTML: split at structural tags, strip markup for cleaner text
        parts = self._HEADING_RE.split(text)
        sections: List[str] = []
        current = ""

        for part in parts:
            if self._HEADING_RE.match(part):
                # Section delimiter — flush current section
                if current.strip():
                    sections.append(current)
                current = part  # start new section with the tag itself
            else:
                current += part

        if current.strip():
            sections.append(current)

        # Convert to plain text and apply overlap
        chunks: List[Chunk] = []
        buffer = ""

        for section_html in sections:
            clean = self._strip_tags(section_html)
            if not clean:
                continue
            candidate = (buffer + " " + clean).strip() if buffer else clean
            if len(candidate) > self.chunk_size and buffer:
                chunks.append(Chunk(text=buffer, index=len(chunks), strategy=self.name, metadata=metadata or {}))
                # Overlap: keep last chunk_overlap chars
                overlap_text = buffer[-self.chunk_overlap:]
                buffer = (overlap_text + " " + clean).strip()
            else:
                buffer = candidate

        if buffer:
            chunks.append(Chunk(text=buffer, index=len(chunks), strategy=self.name, metadata=metadata or {}))

        return chunks


# ---------------------------------------------------------------------------
# 7. SQL Statement Splitter
# ---------------------------------------------------------------------------

class SqlStatementSplitter(BaseSplitter):
    """
    Splits SQL files by statement boundaries.

    Strategy:
      - Normalize line endings.
      - Split on semi-colons (statement terminators).
      - Group multiple small statements into chunks up to `chunk_size`.
      - DDL blocks (CREATE, ALTER, DROP) are always emitted as individual chunks.
    """

    name = "sql_statement"

    _DDL_RE = re.compile(
        r'^\s*(CREATE|ALTER|DROP|TRUNCATE|GRANT|REVOKE)\b',
        re.IGNORECASE | re.MULTILINE,
    )

    def __init__(self, chunk_size: int = 1200):
        self.chunk_size = chunk_size

    def split(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        # Remove comments (-- style and /* */ style)
        clean = re.sub(r'--[^\n]*', '', text)
        clean = re.sub(r'/\*.*?\*/', '', clean, flags=re.DOTALL)

        # Split on semicolons
        raw_statements = clean.split(';')
        statements = [s.strip() for s in raw_statements if s.strip()]

        chunks: List[Chunk] = []
        buffer = ""

        for stmt in statements:
            is_ddl = bool(self._DDL_RE.match(stmt))
            stmt_with_semicolon = stmt + ";"

            if is_ddl:
                # Flush buffer first, then emit DDL alone
                if buffer:
                    chunks.append(Chunk(text=buffer, index=len(chunks), strategy=self.name, metadata=metadata or {}))
                    buffer = ""
                chunks.append(Chunk(
                    text=stmt_with_semicolon,
                    index=len(chunks),
                    strategy=self.name,
                    metadata={**(metadata or {}), "is_ddl": True},
                ))
            elif len(buffer) + len(stmt_with_semicolon) > self.chunk_size and buffer:
                chunks.append(Chunk(text=buffer, index=len(chunks), strategy=self.name, metadata=metadata or {}))
                buffer = stmt_with_semicolon
            else:
                buffer = (buffer + "\n" + stmt_with_semicolon).strip() if buffer else stmt_with_semicolon

        if buffer:
            chunks.append(Chunk(text=buffer, index=len(chunks), strategy=self.name, metadata=metadata or {}))

        return chunks


# ---------------------------------------------------------------------------
# 8. Log Fixed-Window Splitter
# ---------------------------------------------------------------------------

class LogFixedWindowSplitter(BaseSplitter):
    """
    Splits log files using a fixed-size line window with overlap.

    Strategy:
      - Window of `lines_per_chunk` lines (default 50).
      - Overlap of `overlap_lines` lines carried into the next chunk (default 10).
      - Each chunk includes a header comment with line range for traceability.
    """

    name = "log_fixed_window"

    def __init__(self, lines_per_chunk: int = 50, overlap_lines: int = 10):
        self.lines_per_chunk = lines_per_chunk
        self.overlap_lines = overlap_lines

    def split(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        lines = text.splitlines()
        if not lines:
            return []

        step = max(1, self.lines_per_chunk - self.overlap_lines)
        chunks: List[Chunk] = []

        for start in range(0, len(lines), step):
            end = min(start + self.lines_per_chunk, len(lines))
            window = lines[start:end]
            # Prepend line-range comment for traceability in search results
            header = f"# Log lines {start + 1}–{end}"
            chunk_text = header + "\n" + "\n".join(window)
            chunks.append(Chunk(
                text=chunk_text,
                index=len(chunks),
                strategy=self.name,
                metadata={
                    **(metadata or {}),
                    "line_start": start + 1,
                    "line_end": end,
                },
            ))
            if end == len(lines):
                break

        return chunks


# ---------------------------------------------------------------------------
# 9. Markdown Header Splitter
# ---------------------------------------------------------------------------

class MarkdownHeaderSplitter(BaseSplitter):
    """
    Splits Markdown at heading boundaries (# / ## / ### / ####).

    Strategy:
      - Each heading introduces a new section.
      - The heading text is prepended to its own chunk for context.
      - Sections larger than chunk_size are further split with SentenceBoundarySplitter.
    """

    name = "markdown_header"

    _HEADER_RE = re.compile(r'^(#{1,4})\s+(.+)$', re.MULTILINE)

    def __init__(self, chunk_size: int = 900, chunk_overlap: int = 150):
        self.chunk_size = chunk_size
        self.chunk_overlap = chunk_overlap
        self._sentence_splitter = SentenceBoundarySplitter(chunk_size, chunk_overlap)

    def split(self, text: str, metadata: Optional[Dict[str, Any]] = None) -> List[Chunk]:
        # Find all heading positions
        matches = list(self._HEADER_RE.finditer(text))
        if not matches:
            return self._sentence_splitter.split(text, metadata)

        sections: List[Dict[str, Any]] = []
        for i, match in enumerate(matches):
            start = match.start()
            end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
            level = len(match.group(1))
            heading = match.group(2).strip()
            body = text[start:end].strip()
            sections.append({"level": level, "heading": heading, "body": body})

        # Prepend any content before the first heading
        if matches[0].start() > 0:
            preamble = text[:matches[0].start()].strip()
            if preamble:
                sections.insert(0, {"level": 0, "heading": "Preamble", "body": preamble})

        chunks: List[Chunk] = []
        for section in sections:
            body = section["body"]
            if len(body) <= self.chunk_size:
                chunks.append(Chunk(
                    text=body,
                    index=len(chunks),
                    strategy=self.name,
                    metadata={**(metadata or {}), "heading": section["heading"], "level": section["level"]},
                ))
            else:
                # Sub-split oversized sections
                sub_chunks = self._sentence_splitter.split(body, metadata)
                for sc in sub_chunks:
                    sc.metadata["heading"] = section["heading"]
                    sc.metadata["level"] = section["level"]
                    sc.index = len(chunks)
                    sc.strategy = self.name
                    chunks.append(sc)

        return chunks


# ---------------------------------------------------------------------------
# Router — selects the right splitter per file extension
# ---------------------------------------------------------------------------

class SplitterRouter:
    """
    Selects and configures the appropriate splitter based on file extension.

    Routing table:
      Extension group          → Strategy
      ─────────────────────────────────────────────────────────────────────
      .md, .rst                → MarkdownHeaderSplitter
      .txt, plain prose        → SentenceBoundarySplitter
      .py, .js, .ts, .tsx, .jsx,
      .go, .rs, .java, .kt,
      .c, .cpp, .h, .php, .rb  → CodeSlidingWindowSplitter
      .json                    → JsonKeyGroupSplitter
      .yaml, .yml              → JsonKeyGroupSplitter (YAML text fallback)
      .csv                     → CsvRowBatchSplitter
      .html, .htm              → HtmlTagSectionSplitter
      .xml                     → HtmlTagSectionSplitter (XML mode)
      .sql                     → SqlStatementSplitter
      .log                     → LogFixedWindowSplitter
      .sh, .bat, .ps1, .css    → CodeSlidingWindowSplitter (generic)
      .pdf, .docx, .xlsx       → RecursiveCharacterSplitter (binary fallback)
      (unknown)                → RecursiveCharacterSplitter
    """

    _CODE_EXTENSIONS = {
        ".py", ".js", ".ts", ".tsx", ".jsx", ".mjs",
        ".go", ".rs", ".java", ".kt",
        ".c", ".cpp", ".h", ".hpp",
        ".php", ".rb",
        ".sh", ".bat", ".ps1", ".css",
    }

    def get_splitter(self, file_extension: str) -> BaseSplitter:
        ext = file_extension.lower().strip()

        if ext in (".md", ".rst", ".markdown"):
            return MarkdownHeaderSplitter()

        if ext == ".txt":
            return SentenceBoundarySplitter()

        if ext in self._CODE_EXTENSIONS:
            return CodeSlidingWindowSplitter(file_extension=ext)

        if ext == ".json":
            return JsonKeyGroupSplitter(file_extension=ext)

        if ext in (".yaml", ".yml"):
            return JsonKeyGroupSplitter(file_extension=ext)

        if ext == ".csv":
            return CsvRowBatchSplitter()

        if ext in (".html", ".htm"):
            return HtmlTagSectionSplitter(file_extension=ext)

        if ext == ".xml":
            return HtmlTagSectionSplitter(file_extension=ext)

        if ext == ".sql":
            return SqlStatementSplitter()

        if ext == ".log":
            return LogFixedWindowSplitter()

        # Binary-extracted or unknown → generic recursive splitter
        return RecursiveCharacterSplitter()

    def split(
        self,
        text: str,
        file_extension: str = "",
        metadata: Optional[Dict[str, Any]] = None,
    ) -> List[Chunk]:
        """Convenience: route + split in one call."""
        splitter_instance = self.get_splitter(file_extension)
        logger.debug(
            "SplitterRouter: ext=%s → strategy=%s",
            file_extension,
            splitter_instance.name,
        )
        return splitter_instance.split(text, metadata)


# ---------------------------------------------------------------------------
# Module-level singleton (backward compatible with existing `splitter` usage)
# ---------------------------------------------------------------------------

# Legacy singleton for simple usage (generic splitter, no routing)
splitter = RecursiveCharacterSplitter()

# Smart router singleton — use this for new code
router = SplitterRouter()
