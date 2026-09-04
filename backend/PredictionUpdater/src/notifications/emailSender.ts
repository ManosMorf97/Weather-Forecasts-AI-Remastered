import nodemailer, { type Transporter } from 'nodemailer';
import type { Config } from '../config.js';
import { logger } from '../logging/logger.js';

export interface EmailSender {
  send(to: string, subject: string, body: string): Promise<void>;
}

// Real delivery over SMTP. Mirrors WeatherUserActions' SmtpEmailSender.
class SmtpEmailSender implements EmailSender {
  private readonly transport: Transporter;

  constructor(private readonly from: string, config: Config) {
    this.transport = nodemailer.createTransport({
      host: config.SMTP_HOST,
      port: config.SMTP_PORT,
      secure: config.SMTP_PORT === 465,
      auth: config.SMTP_USER ? { user: config.SMTP_USER, pass: config.SMTP_PASSWORD } : undefined,
    });
  }

  async send(to: string, subject: string, body: string): Promise<void> {
    await this.transport.sendMail({ from: this.from, to, subject, text: body });
  }
}

// Fallback: log the message instead of sending. Lets the job run end-to-end with no mail server.
class LoggingEmailSender implements EmailSender {
  send(to: string, subject: string, body: string): Promise<void> {
    logger.info({ to, subject, body }, 'email (logging transport - not sent)');
    return Promise.resolve();
  }
}

export function buildEmailSender(config: Config): EmailSender {
  return config.SMTP_HOST
    ? new SmtpEmailSender(config.EMAIL_FROM, config)
    : new LoggingEmailSender();
}
