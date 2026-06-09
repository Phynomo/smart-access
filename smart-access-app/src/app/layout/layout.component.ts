import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from './header/header.component';
import { BottomNavComponent } from './bottom-nav/bottom-nav.component';

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, HeaderComponent, BottomNavComponent],
  templateUrl: './layout.component.html',
})
export class LayoutComponent {}
