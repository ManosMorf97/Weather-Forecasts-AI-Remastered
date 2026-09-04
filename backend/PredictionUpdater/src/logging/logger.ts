import pino from 'pino';

// One process-wide logger. Pretty output in dev, JSON lines in production (NODE_ENV=production).
export const logger = pino({
  level: process.env.LOG_LEVEL ?? 'info',
  transport:
    process.env.NODE_ENV === 'production'
      ? undefined
      : { target: 'pino-pretty', options: { translateTime: 'SYS:standard', ignore: 'pid,hostname' } },
});
