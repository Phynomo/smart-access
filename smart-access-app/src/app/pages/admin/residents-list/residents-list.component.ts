import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { ButtonModule } from 'primeng/button';
import { ResidentService } from '../../../core/services/resident.service';
import { ApiService } from '../../../core/services/api.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ResidentResponse } from '../../../core/models/resident.models';

@Component({
  selector: 'app-residents-list',
  imports: [FormsModule, InputTextModule, IconFieldModule, InputIconModule, ButtonModule],
  templateUrl: './residents-list.component.html',
})
export class ResidentsListComponent {
  private readonly residentService = inject(ResidentService);
  private readonly api = inject(ApiService);
  private readonly notify = inject(NotificationService);
  private readonly router = inject(Router);

  /** URL completa de la foto del residente (o null) a partir de la ruta relativa. */
  photoSrc(r: ResidentResponse): string | null {
    return this.api.mediaUrl(r.photoUrl);
  }

  readonly residents = signal<ResidentResponse[]>([]);
  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly search = signal('');

  // Filtros por estado (dos chips). Cada residente es activo o inactivo.
  readonly showActive = signal(true);
  readonly showInactive = signal(true);
  readonly activeCount = computed(() => this.residents().filter((r) => r.isActive).length);
  readonly inactiveCount = computed(() => this.residents().filter((r) => !r.isActive).length);

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const active = this.showActive();
    const inactive = this.showInactive();
    return this.residents().filter((r) => {
      // Si no hay ningún chip activo, no filtramos por estado (mostramos todos).
      const byStatus = !active && !inactive ? true : r.isActive ? active : inactive;
      if (!byStatus) return false;
      if (!q) return true;
      return (
        r.name.toLowerCase().includes(q) ||
        r.houseNumber.toLowerCase().includes(q) ||
        r.email.toLowerCase().includes(q)
      );
    });
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    // false → trae activos e inactivos (el filtro fino se hace con los chips).
    this.residentService.getAll(false).subscribe({
      next: (list) => this.residents.set(list),
      error: () => {
        this.loadFailed.set(true);
        this.notify.error('No se pudieron cargar los residentes.');
      },
      complete: () => this.loading.set(false),
    });
  }

  goToRegister(): void {
    this.router.navigate(['/admin/residentes/nuevo']);
  }

  goToEdit(id: string): void {
    this.router.navigate(['/admin/residentes', id, 'editar']);
  }

  initials(name: string): string {
    return (
      name
        .split(' ')
        .map((w) => w[0] ?? '')
        .join('')
        .slice(0, 2)
        .toUpperCase() || 'R'
    );
  }
}
