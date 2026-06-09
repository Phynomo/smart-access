import { Injectable } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { AccessControl, NativeBiometric } from '@capgo/capacitor-native-biometric';

export interface StoredCredentials {
  identifier: string;
  password: string;
}

/**
 * Envuelve el plugin nativo de biometría (Face ID / Touch ID / huella Android).
 *
 * El plugin guarda las credenciales en el almacenamiento seguro del SO
 * (Keychain en iOS, Keystore/EncryptedSharedPreferences en Android). El acceso
 * a `getCredentials` se protege detrás de `verifyIdentity`, que dispara el
 * prompt biométrico del sistema.
 *
 * En web (ng serve) no hay biometría: todos los métodos degradan a "no
 * disponible" sin lanzar, para que el desarrollo siga funcionando.
 */
@Injectable({ providedIn: 'root' })
export class BiometricService {
  /** Identifica el conjunto de credenciales en el llavero del SO. */
  private readonly server = 'com.phynomo.smartacess';
  /** Flag local: el usuario activó el inicio biométrico. */
  private readonly ENABLED_KEY = 'rp-biometric-enabled';

  /** ¿El dispositivo tiene biometría configurada y disponible? */
  async isAvailable(): Promise<boolean> {
    if (!Capacitor.isNativePlatform()) return false;
    try {
      const result = await NativeBiometric.isAvailable();
      return result.isAvailable;
    } catch {
      return false;
    }
  }

  /** ¿El usuario ya activó el inicio biométrico en este dispositivo? */
  isEnabled(): boolean {
    return localStorage.getItem(this.ENABLED_KEY) === 'true';
  }

  /**
   * Guarda las credenciales tras un login exitoso y marca la biometría como activa.
   * `BIOMETRY_ANY` liga la clave de cifrado a la biometría del dispositivo (la
   * credencial solo se puede desencriptar tras una verificación biométrica) y
   * sobrevive al alta de nuevas huellas/rostros, a diferencia de CURRENT_SET.
   */
  async enable(identifier: string, password: string): Promise<void> {
    await NativeBiometric.setCredentials({
      username: identifier,
      password,
      server: this.server,
      accessControl: AccessControl.BIOMETRY_ANY,
    });
    localStorage.setItem(this.ENABLED_KEY, 'true');
  }

  /** Borra las credenciales guardadas y desactiva el inicio biométrico. */
  async disable(): Promise<void> {
    try {
      await NativeBiometric.deleteCredentials({ server: this.server });
    } catch {
      // Si no había credenciales guardadas, ignoramos el error.
    }
    localStorage.removeItem(this.ENABLED_KEY);
  }

  /**
   * Lanza el prompt biométrico y, si se verifica, desencripta y devuelve las
   * credenciales guardadas en un solo paso (la desencriptación está ligada a la
   * biometría: no se puede recuperar sin pasar la verificación). Rechaza si el
   * usuario cancela o la verificación falla.
   */
  async authenticate(): Promise<StoredCredentials> {
    const creds = await NativeBiometric.getSecureCredentials({
      server: this.server,
      reason: 'Inicia sesión en ResidentPass',
      title: 'Inicio de sesión',
      subtitle: 'Verifica tu identidad',
      negativeButtonText: 'Cancelar',
    });
    return { identifier: creds.username, password: creds.password };
  }
}
