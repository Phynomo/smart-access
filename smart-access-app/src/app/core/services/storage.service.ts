import { Injectable } from '@angular/core';
import { Preferences } from '@capacitor/preferences';

/**
 * Wrapper sobre @capacitor/preferences.
 * Usa NSUserDefaults (iOS), SharedPreferences (Android) y localStorage (web).
 * API async — no bloquea el hilo principal.
 */
@Injectable({ providedIn: 'root' })
export class StorageService {
  async set(key: string, value: string): Promise<void> {
    await Preferences.set({ key, value });
  }

  async get(key: string): Promise<string | null> {
    const { value } = await Preferences.get({ key });
    return value;
  }

  async remove(key: string): Promise<void> {
    await Preferences.remove({ key });
  }

  async clear(): Promise<void> {
    await Preferences.clear();
  }

  // ── Helpers para JSON ──────────────────────────────────────────────────────

  async setJson<T>(key: string, value: T): Promise<void> {
    await this.set(key, JSON.stringify(value));
  }

  async getJson<T>(key: string): Promise<T | null> {
    const raw = await this.get(key);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as T;
    } catch {
      return null;
    }
  }
}
