import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";
import eslintConfigPrettier from "eslint-config-prettier";
import simpleImportSort from "eslint-plugin-simple-import-sort";

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  {
    // Pin the React version explicitly. eslint-plugin-react (pulled transitively by
    // eslint-config-next) auto-detects the React version via context.getFilename(),
    // which ESLint 10 removed — auto-detection crashes on ESLint 10. Declaring the
    // version here skips that code path. Keep in sync with the installed react version.
    settings: {
      react: {
        version: "19.2",
      },
    },
    plugins: {
      "simple-import-sort": simpleImportSort,
    },
    rules: {
      "@typescript-eslint/no-explicit-any": "error",
      "@typescript-eslint/no-non-null-assertion": "error",
      "simple-import-sort/imports": "error",
      "simple-import-sort/exports": "error",
      "react/no-array-index-key": "warn",
    },
  },
  {
    // Exported utility functions and non-JSX modules must have explicit return types.
    // Omitted for .tsx because TypeScript reliably infers React component return types.
    files: ["**/*.ts"],
    rules: {
      "@typescript-eslint/explicit-module-boundary-types": "error",
    },
  },
  eslintConfigPrettier,
  globalIgnores([".next/**", "out/**", "build/**", "next-env.d.ts", "lib/api/generated/**"]),
]);

export default eslintConfig;
