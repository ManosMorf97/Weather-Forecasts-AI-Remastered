import { Account, Client } from 'appwrite';

const endpoint = import.meta.env.YOUR_APPWRITE_ENDPOINT;
const projectId = import.meta.env.YOUR_APPWRITE_PROJECT_ID;

if (!endpoint || !projectId) {
  throw new Error(
    'Appwrite is not configured. Set the YOUR_APPWRITE_ENDPOINT and YOUR_APPWRITE_PROJECT_ID ' +
      'environment variables (the same ones WeatherUserActions reads).',
  );
}

export const appwriteClient = new Client().setEndpoint(endpoint).setProject(projectId);
export const account = new Account(appwriteClient);
