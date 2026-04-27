import { Routes } from '@angular/router';
import { AppComponent } from './app.component';
import { ConsentComponent } from './consent/consent.component';

export const routes: Routes = [
  {
    path: '',
    component: AppComponent
  },
  {
    path: 'consent',
    component: ConsentComponent
  },
  {
    path: '**',
    redirectTo: ''
  }
];
