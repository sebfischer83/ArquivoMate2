import { Routes } from '@angular/router';
import { LandingPageComponent } from './landing/landingPage/landingPage.component';
import { AuthGuard } from './guards/auth.guard';

export const routes: Routes = [
    { path: '', component: LandingPageComponent },
    {
        path: 'app',
        canActivate: [AuthGuard],
        loadComponent: () =>
            import('./main/main-area/main-area.component').then(m => m.MainAreaComponent)
    }
];
