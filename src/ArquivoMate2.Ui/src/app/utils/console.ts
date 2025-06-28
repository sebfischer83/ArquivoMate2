import { isDevMode } from '@angular/core';

/**
 * Global console utility that only logs in development mode
 */
export class DevConsole {
  static log(...args: any[]): void {
    if (isDevMode()) {
      console.log(...args);
    }
  }

  static warn(...args: any[]): void {
    if (isDevMode()) {
      console.warn(...args);
    }
  }

  static error(...args: any[]): void {
    if (isDevMode()) {
      console.error(...args);
    }
  }

  static info(...args: any[]): void {
    if (isDevMode()) {
      console.info(...args);
    }
  }

  static debug(...args: any[]): void {
    if (isDevMode()) {
      console.debug(...args);
    }
  }

  static table(data: any, columns?: string[]): void {
    if (isDevMode()) {
      console.table(data, columns);
    }
  }

  static group(label?: string): void {
    if (isDevMode()) {
      console.group(label);
    }
  }

  static groupEnd(): void {
    if (isDevMode()) {
      console.groupEnd();
    }
  }

  static time(label?: string): void {
    if (isDevMode()) {
      console.time(label);
    }
  }

  static timeEnd(label?: string): void {
    if (isDevMode()) {
      console.timeEnd(label);
    }
  }
}
