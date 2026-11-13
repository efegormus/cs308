import assert from "node:assert/strict";
import test from "node:test";
import {
  PROFILE_FIELDS, continueLink, isProfileIncomplete, profileFieldErrors, profileHref, profileRequestBody, profileValues,
  validateProfile, validateProfileTaxId
} from "./profile-form.js";

const complete = { id: 3, name: "Ada", email: "ada@example.com", taxId: "10000000146", homeAddress: "10 Example Street" };

test("the form starts from the stored values, and blank stays blank", () => {
  assert.deepEqual(profileValues(complete), { name: "Ada", taxId: "10000000146", homeAddress: "10 Example Street" });
  assert.deepEqual(profileValues({ ...complete, taxId: "", homeAddress: "" }), { name: "Ada", taxId: "", homeAddress: "" });
  assert.deepEqual(profileValues(null), { name: "", taxId: "", homeAddress: "" });
  assert.deepEqual(PROFILE_FIELDS, ["name", "taxId", "homeAddress"]);
});

test("a profile without tax ID or home address is incomplete", () => {
  assert.equal(isProfileIncomplete(complete), false);
  assert.equal(isProfileIncomplete({ ...complete, taxId: "" }), true);
  assert.equal(isProfileIncomplete({ ...complete, homeAddress: "   " }), true);
  assert.equal(isProfileIncomplete({ ...complete, taxId: undefined }), true);
});

test("validateProfile reports empty and too long fields with the backend's limits", () => {
  assert.deepEqual(validateProfile(complete), { name: "", taxId: "", homeAddress: "" });
  assert.deepEqual(validateProfile({ name: " ", taxId: "", homeAddress: "\n" }), {
    name: "Enter your full name.", taxId: "Enter your tax ID.", homeAddress: "Enter your home address."
  });
  assert.deepEqual(validateProfile({ name: "a".repeat(101), taxId: "1".repeat(65), homeAddress: "a".repeat(1001) }), {
    name: "Use at most 100 characters.", taxId: "Use at most 64 characters.", homeAddress: "Use at most 1000 characters."
  });
  // Counted after trimming, as the backend does.
  assert.equal(validateProfile({ ...complete, name: ` ${"a".repeat(100)} ` }).name, "");
});

test("the profile's tax ID check accepts older tax IDs that are not TC kimlik numbers", () => {
  assert.equal(validateProfileTaxId("TAX-123"), "");
  assert.equal(validateProfileTaxId(" 12345678901 "), "");
  assert.equal(validateProfileTaxId("1".repeat(64)), "");
});

test("the request body holds the three fields, trimmed", () => {
  const body = profileRequestBody({ name: "  Ada ", taxId: " 10000000146\t", homeAddress: "\n10 Street\n", email: "x@example.com" });
  assert.deepEqual(body, { name: "Ada", taxId: "10000000146", homeAddress: "10 Street" });
});

test("server field messages are mapped to the fields; unknown text and other fields are not shown", () => {
  assert.deepEqual(profileFieldErrors({
    name: ["This field is required."],
    taxId: ["Use at most 64 characters."],
    homeAddress: ["This value is too long when encoded as UTF-8."],
    email: ["Enter a valid email address without a display name."],
    role: ["SECRET"]
  }), {
    name: "This field is required.",
    taxId: "Use at most 64 characters.",
    homeAddress: "This value is too long when encoded as UTF-8."
  });
  assert.deepEqual(profileFieldErrors({ name: ["<b>SECRET</b>"], taxId: [42], homeAddress: [] }),
    { name: "Check this field.", taxId: "Check this field." });
  assert.deepEqual(profileFieldErrors(null), {});
});

test("after a save a safe returnUrl gets a Continue link; checkout gets its own label", () => {
  assert.deepEqual(continueLink("/checkout"), { href: "/checkout", label: "Continue to checkout" });
  assert.deepEqual(continueLink("/checkout/"), { href: "/checkout/", label: "Continue to checkout" });
  assert.deepEqual(continueLink("/cart?from=profile"), { href: "/cart?from=profile", label: "Continue" });
});

test("no Continue link for external, unsafe, home or looping returnUrl values", () => {
  for (const value of [null, "", "/", "https://evil.example/checkout", "//evil.example/checkout", "/\\evil.example",
    "javascript:alert(1)", "/%2f%2fevil.example", "/login?returnUrl=/checkout", "/profile", "/profile?returnUrl=/checkout"]) {
    assert.equal(continueLink(value), null, value);
  }
});

test("checkout sends an incomplete profile to /profile with an encoded, safe returnUrl", () => {
  assert.equal(profileHref("/checkout"), "/profile?returnUrl=%2Fcheckout");
  assert.equal(profileHref("/checkout?step=2"), "/profile?returnUrl=%2Fcheckout%3Fstep%3D2");
  assert.equal(profileHref("https://evil.example"), "/profile");
});
