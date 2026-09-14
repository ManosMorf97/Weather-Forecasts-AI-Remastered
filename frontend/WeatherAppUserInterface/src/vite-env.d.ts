/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly YOUR_APPWRITE_ENDPOINT: string;
  readonly YOUR_APPWRITE_PROJECT_ID: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
