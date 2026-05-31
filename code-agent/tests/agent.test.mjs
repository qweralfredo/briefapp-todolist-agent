import test from "node:test";
import assert from "node:assert/strict";
import { createSession, getSession, listAvailableSkills } from "../src/backend/agent.mjs";

test("createSession initializes an isolated coding session", () => {
  const session = createSession({
    title: "Unit Test Session",
    model: "demo-model",
    workspaceName: "default",
  });

  const loaded = getSession(session.id);
  assert.equal(loaded.title, "Unit Test Session");
  assert.equal(loaded.model, "demo-model");
  assert.deepEqual(loaded.plan, []);
});

test("listAvailableSkills exposes skill metadata", () => {
  const skills = listAvailableSkills();
  assert.ok(skills.every((skill) => typeof skill.description === "string"));
  assert.ok(skills.length >= 2);
});
