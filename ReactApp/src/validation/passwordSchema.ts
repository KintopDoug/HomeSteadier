import { z } from "zod";

/**
 * Mirrors the API's Identity password options (Program.cs: RequiredLength = 8,
 * RequireNonAlphanumeric = false) plus the upper/lower/digit rules the sign-up form has always
 * enforced client-side. Shared by sign-up, password reset, and change password so the three
 * can't drift apart — a rule that only exists on one of them is a form that rejects a password
 * the API would have accepted, or vice versa.
 */
export const passwordSchema = z
  .string()
  .min(8, "Password must be at least 8 characters")
  .regex(/[A-Z]/, "Password must contain at least 1 upper case letter")
  .regex(/[a-z]/, "Password must contain at least 1 lower case letter")
  .regex(/[0-9]/, "Password must contain at least 1 number");
