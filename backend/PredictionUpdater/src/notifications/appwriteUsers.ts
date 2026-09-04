import { Client, Query, Users } from 'node-appwrite';
import type { Config } from '../config.js';
import { logger } from '../logging/logger.js';

export interface UsersLookup {
  // userId -> email, for the ids that resolved. Missing ids are simply absent from the map.
  getEmails(userIds: string[]): Promise<Map<string, string>>;
}

const MAX_RETRIES = 3;

// UC11 step 11: batch lookup against the Appwrite server API (an API key, not a user JWT).
// Emails are never stored locally - the Authentication Service is the only source.
export class AppwriteUsersLookup implements UsersLookup {
  private readonly users: Users;

  constructor(private readonly config: Config) {
    const client = new Client()
      .setEndpoint(required(config.YOUR_APPWRITE_ENDPOINT, 'YOUR_APPWRITE_ENDPOINT'))
      .setProject(required(config.YOUR_APPWRITE_PROJECT_ID, 'YOUR_APPWRITE_PROJECT_ID'))
      .setKey(required(config.YOUR_APPWRITE_API_KEY, 'YOUR_APPWRITE_API_KEY'));
    this.users = new Users(client);
  }

  async getEmails(userIds: string[]): Promise<Map<string, string>> {
    const result = new Map<string, string>();

    for (const chunk of chunked(userIds, this.config.APPWRITE_ID_CHUNK_SIZE)) {
      try {
        const page = await withRetry(() =>
          this.users.list([Query.equal('$id', chunk), Query.limit(chunk.length)]),
        );
        for (const user of page.users) {
          if (user.email) result.set(user.$id, user.email);
        }
      } catch (err) {
        // A8: this chunk stays unresolved; those users are retried next cycle.
        logger.error({ err, chunkSize: chunk.length }, 'Appwrite user batch lookup failed');
      }
    }

    return result;
  }
}

function required(value: string | undefined, name: string): string {
  if (!value) throw new Error(`${name} is required for danger notifications`);
  return value;
}

function chunked<T>(items: T[], size: number): T[][] {
  const chunks: T[][] = [];
  for (let i = 0; i < items.length; i += size) chunks.push(items.slice(i, i + size));
  return chunks;
}

async function withRetry<T>(fn: () => Promise<T>): Promise<T> {
  let lastError: unknown;
  for (let attempt = 1; attempt <= MAX_RETRIES; attempt++) {
    try {
      return await fn();
    } catch (err) {
      lastError = err;
      if (attempt < MAX_RETRIES) {
        await delay(250 * 2 ** (attempt - 1));
      }
    }
  }
  throw lastError;
}

const delay = (ms: number): Promise<void> => new Promise((resolve) => setTimeout(resolve, ms));
