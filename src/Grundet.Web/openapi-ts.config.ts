import { defineConfig } from "@hey-api/openapi-ts";

export default defineConfig({
  input: process.env.OPENAPI_SPEC_URL ?? "http://api.grundet.test:8080/openapi/v1.json",
  output: {
    path: "lib/api/generated",
    postProcess: ["prettier"],
  },
  plugins: ["@hey-api/client-fetch"],
});
