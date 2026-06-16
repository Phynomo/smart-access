import { Injectable } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { Filesystem, Directory } from '@capacitor/filesystem';
import { Share } from '@capacitor/share';

@Injectable({ providedIn: 'root' })
export class QrShareService {
  async shareFromElement(container: HTMLElement, title: string, subtitle = ''): Promise<boolean> {
    const source = container.querySelector('canvas');
    if (!source) return false;

    const composed = this.compose(source, title, subtitle);
    const text = subtitle ? `${title} · ${subtitle} — Smart Access` : `${title} — Smart Access`;
    const fileName = `residentpass-${this.slug(title)}.png`;

    try {
      if (Capacitor.isNativePlatform()) {
        return await this.shareNative(composed, fileName, text);
      }
      return await this.shareWeb(composed, fileName, text);
    } catch {
      return false;
    }
  }

  // ── Capacitor (Android / iOS) ─────────────────────────────────────────────

  private async shareNative(canvas: HTMLCanvasElement, fileName: string, text: string): Promise<boolean> {
    const base64 = canvas.toDataURL('image/png').split(',')[1];

    const saved = await Filesystem.writeFile({
      path: fileName,
      data: base64,
      directory: Directory.Cache,
    });

    await Share.share({
      title: 'Código QR · Smart Access',
      text,
      files: [saved.uri],
      dialogTitle: 'Compartir código QR',
    });

    return true;
  }

  // ── Web Share API (PWA / navegador) ──────────────────────────────────────

  private async shareWeb(canvas: HTMLCanvasElement, fileName: string, text: string): Promise<boolean> {
    const blob = await this.canvasToBlob(canvas);
    if (!blob) return false;

    const file = new File([blob], fileName, { type: 'image/png' });
    const nav = navigator as Navigator & {
      canShare?: (data?: unknown) => boolean;
      share?: (data?: unknown) => Promise<void>;
    };

    if (nav.canShare?.({ files: [file] }) && nav.share) {
      await nav.share({ files: [file], title: 'Código QR · Smart Access', text });
      return true;
    }

    if (nav.share) {
      await nav.share({ title: 'Código QR · Smart Access', text });
      return true;
    }

    this.download(blob, fileName);
    return true;
  }

  // ── Canvas composition ────────────────────────────────────────────────────

  private compose(source: HTMLCanvasElement, title: string, subtitle: string): HTMLCanvasElement {
    const W = 640;
    const H = 820;
    const canvas = document.createElement('canvas');
    canvas.width = W;
    canvas.height = H;
    const ctx = canvas.getContext('2d');
    if (!ctx) return source;

    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, W, H);
    ctx.fillStyle = '#2563eb';
    ctx.fillRect(0, 0, W, 14);

    ctx.textAlign = 'center';

    ctx.fillStyle = '#0f172a';
    ctx.font = '700 46px Arial, sans-serif';
    ctx.fillText('Smart Access', W / 2, 100);
    ctx.fillStyle = '#64748b';
    ctx.font = '400 24px Arial, sans-serif';
    ctx.fillText('Control de acceso residencial', W / 2, 140);

    const qrSize = 400;
    const qx = (W - qrSize) / 2;
    const qy = 196;
    ctx.fillStyle = '#f8fafc';
    this.roundRect(ctx, qx - 24, qy - 24, qrSize + 48, qrSize + 48, 28);
    ctx.fill();
    ctx.strokeStyle = '#e2e8f0';
    ctx.lineWidth = 2;
    this.roundRect(ctx, qx - 24, qy - 24, qrSize + 48, qrSize + 48, 28);
    ctx.stroke();
    ctx.drawImage(source, qx, qy, qrSize, qrSize);

    let cursorY = qy + qrSize + 92;
    ctx.fillStyle = '#0f172a';
    ctx.font = '600 34px Arial, sans-serif';
    ctx.fillText(this.truncate(ctx, title, W - 80), W / 2, cursorY);

    if (subtitle) {
      cursorY += 40;
      ctx.fillStyle = '#64748b';
      ctx.font = '400 24px Arial, sans-serif';
      ctx.fillText(subtitle, W / 2, cursorY);
    }

    ctx.fillStyle = '#94a3b8';
    ctx.font = '400 22px Arial, sans-serif';
    ctx.fillText('Presenta este código en la caseta de acceso', W / 2, H - 44);

    return canvas;
  }

  private roundRect(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number): void {
    if (typeof ctx.roundRect === 'function') {
      ctx.beginPath();
      ctx.roundRect(x, y, w, h, r);
      return;
    }
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
  }

  private truncate(ctx: CanvasRenderingContext2D, text: string, maxWidth: number): string {
    if (ctx.measureText(text).width <= maxWidth) return text;
    let t = text;
    while (t.length > 1 && ctx.measureText(t + '…').width > maxWidth) t = t.slice(0, -1);
    return t + '…';
  }

  private canvasToBlob(canvas: HTMLCanvasElement): Promise<Blob | null> {
    return new Promise((resolve) => canvas.toBlob((b) => resolve(b), 'image/png'));
  }

  private download(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    a.click();
    URL.revokeObjectURL(url);
  }

  private slug(text: string): string {
    return text.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'codigo';
  }
}
