// Every data shape the pipeline passes between its steps. The step functions live in
// poll.ts / store.ts / notify.ts / run.ts and import their input/output types from here.

import type { EmailSender } from '../notifications/emailSender.js';
import type { UsersLookup } from '../notifications/appwriteUsers.js';
import type { ForecastsRepository } from '../repositories/forecastsRepository.js';
import type { NotificationsRepository } from '../repositories/notificationsRepository.js';
import type { SelectionsRepository } from '../repositories/selectionsRepository.js';
import type { NormalizedForecast, WeatherProvider } from '../providers/types.js';

// What one (city site, service) pair returned from a single poll.
export interface CitySiteForecasts {
  citySiteId: number;
  serviceName: string;
  forecasts: NormalizedForecast[];
}

// Outcome of the poll step (UC11 steps 3-6).
export interface PollingResult {
  servicesAttempted: number;
  servicesSucceeded: number;
  failedServices: string[];
  results: CitySiteForecasts[];
}

// Outcome of the store step (UC11 step 7, A4/A5).
export interface StorageResult {
  inserted: number;
  updated: number;
  skipped: number;
}

// Outcome of the notify step (UC11 steps 8-13).
export interface NotificationResult {
  dangerForecasts: number;
  usersWithWarnings: number;
  usersNotified: number;
  deliveriesFailed: number;
  emailsMissing: number;
}

// Everything runCycle needs injected (composition root wires these in main.ts).
export interface CycleDeps {
  registry: Map<string, WeatherProvider>;
  selectionsRepo: SelectionsRepository;
  forecastsRepo: ForecastsRepository;
  notificationsRepo: NotificationsRepository;
  users: UsersLookup;
  email: EmailSender;
}

// Outcome of one full cycle.
export interface CycleResult {
  selections: number;
  poll?: PollingResult;
  store?: StorageResult;
  notify?: NotificationResult;
  // E1: every attempted service failed this cycle.
  allServicesFailed: boolean;
}
