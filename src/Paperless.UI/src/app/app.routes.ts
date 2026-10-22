import { Routes } from '@angular/router';
import { Dashboard } from './pages/dashboard/dashboard';
import { DocumentDetail } from './pages/document-detail/document-detail';

export const routes: Routes = [
  { path: '', component: Dashboard },
  { path: 'documents/:id', component: DocumentDetail },
  { path: '**', redirectTo: '' },
];
