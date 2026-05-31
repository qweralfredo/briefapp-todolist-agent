import test from "node:test";
import assert from "node:assert/strict";
import { readFile, resolveTarget, writeFile } from "../src/backend/workspace.mjs";

test("workspace write and read stay inside the sandbox", () => {
  const writeResult = writeFile({
    workspaceName: "test-sandbox",
    target: "notes/todo.txt",
    content: "sandboxed content",
  });

  assert.equal(writeResult.path, "notes/todo.txt");

  const readResult = readFile({
    workspaceName: "test-sandbox",
    target: "notes/todo.txt",
  });

  assert.equal(readResult.content, "sandboxed content");
});

test("resolveTarget rejects traversal outside workspace", () => {
  assert.throws(
    () => resolveTarget("c:\\safe-root", "..\\escape.txt"),
    /outside the allowed workspace/i,
  );
});
