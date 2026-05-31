import test from "node:test";
import assert from "node:assert/strict";
import { discoverSkills, parseFrontmatter } from "../src/backend/skills.mjs";

test("parseFrontmatter extracts attributes and body", () => {
  const parsed = parseFrontmatter(`---\nname: demo\ndescription: sample\n---\n\n# Body`);
  assert.equal(parsed.attributes.name, "demo");
  assert.equal(parsed.attributes.description, "sample");
  assert.match(parsed.body, /# Body/);
});

test("discoverSkills returns bundled local skills", () => {
  const skills = discoverSkills();
  const names = skills.map((skill) => skill.name);
  assert.ok(names.includes("core-coding"));
  assert.ok(names.includes("web-ui"));
});
