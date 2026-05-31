import pytest
from app.services.splitter import (
    SplitterRouter,
    RecursiveCharacterSplitter,
    SentenceBoundarySplitter,
    CodeSlidingWindowSplitter,
    JsonKeyGroupSplitter,
    CsvRowBatchSplitter,
    HtmlTagSectionSplitter,
    SqlStatementSplitter,
    LogFixedWindowSplitter,
    MarkdownHeaderSplitter
)

def test_recursive_character_splitter():
    splitter = RecursiveCharacterSplitter(chunk_size=10, chunk_overlap=2)
    chunks = splitter.split("aa bb cc dd ee")
    assert len(chunks) > 0
    assert "aa" in chunks[0].text

def test_sentence_boundary_splitter():
    splitter = SentenceBoundarySplitter(chunk_size=50, chunk_overlap=10, overlap_sentences=1)
    text = "Hello world. This is a sentence.\n\nAnother paragraph."
    chunks = splitter.split(text)
    assert len(chunks) > 0
    assert chunks[0].strategy == "sentence_boundary"

def test_code_sliding_window_splitter():
    splitter = CodeSlidingWindowSplitter(chunk_size_lines=5, overlap_lines=2, file_extension=".py")
    text = "def a():\n  pass\n\ndef b():\n  pass\n"
    chunks = splitter.split(text)
    assert len(chunks) > 0
    assert chunks[-1].metadata.get("language_family") == "python"

def test_json_key_group_splitter():
    splitter = JsonKeyGroupSplitter(chunk_size=100)
    text = '{"a": 1, "b": 2}'
    chunks = splitter.split(text)
    assert len(chunks) > 0

def test_json_key_group_splitter_fallback():
    splitter = JsonKeyGroupSplitter()
    chunks = splitter.split("invalid json")
    assert chunks[0].strategy == "recursive_character"

def test_yaml_fallback():
    splitter = JsonKeyGroupSplitter(file_extension=".yaml")
    chunks = splitter.split("a: 1")
    assert getattr(chunks[0], "strategy", "") == "sentence_boundary"

def test_csv_row_batch_splitter():
    splitter = CsvRowBatchSplitter(rows_per_chunk=2)
    text = "col1,col2\n1,2\n3,4\n5,6"
    chunks = splitter.split(text)
    assert len(chunks) == 2
    assert "col1,col2" in chunks[0].text
    assert "col1,col2" in chunks[1].text

def test_html_tag_section_splitter():
    splitter = HtmlTagSectionSplitter(chunk_size=50)
    text = "<h1>Title</h1><p>content</p><h2>Subtitle</h2><p>more</p>"
    chunks = splitter.split(text)
    assert len(chunks) > 0

def test_html_xml_splitter():
    splitter = HtmlTagSectionSplitter(chunk_size=50, file_extension=".xml")
    text = "<root><item>1</item></root>"
    chunks = splitter.split(text)
    assert len(chunks) > 0

def test_sql_statement_splitter():
    splitter = SqlStatementSplitter(chunk_size=50)
    text = "SELECT * FROM a; CREATE TABLE b (id int);"
    chunks = splitter.split(text)
    assert len(chunks) > 0
    assert any(c.metadata.get("is_ddl") for c in chunks)

def test_log_fixed_window_splitter():
    splitter = LogFixedWindowSplitter(lines_per_chunk=2, overlap_lines=1)
    text = "l1\nl2\nl3\nl4"
    chunks = splitter.split(text)
    assert len(chunks) == 3
    assert "# Log lines" in chunks[0].text

def test_markdown_header_splitter():
    splitter = MarkdownHeaderSplitter(chunk_size=50)
    text = "# H1\ncontent 1\n## H2\ncontent 2"
    chunks = splitter.split(text)
    assert len(chunks) > 0
    assert chunks[0].metadata.get("level") == 1

def test_splitter_router():
    router = SplitterRouter()
    chunks = router.split("# some text", ".md")
    assert len(chunks) > 0
    assert chunks[0].strategy == "markdown_header"
    
    chunks = router.split("some text", ".unknown")
    assert chunks[0].strategy == "recursive_character"
