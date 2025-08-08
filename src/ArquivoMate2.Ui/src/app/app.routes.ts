import { Routes } from '@angular/router';
import { LandingPageComponent } from './landing/landingPage/landingPage.component';
import { AuthGuard } from './guards/auth.guard';

export const routes: Routes = [
    { path: '', component: LandingPageComponent },
    {
        path: 'app',
        canActivate: [AuthGuard],
        loadComponent: () =>
            import('./main/main-area/main-area.page').then(m => m.MainAreaComponent),
        children: [
            {
                path: '',
                redirectTo: 'dashboard',
                pathMatch: 'full'
            },
            {
                path: 'dashboard',
                loadComponent: () =>
                    import('./main/pages/dashboard/dashboard.component').then(m => m.DashboardComponent)
            },
            {
                path: 'import-history',
                loadComponent: () =>
                    import('./main/pages/import-history/import-history.component').then(m => m.ImportHistoryComponent)
            }
        ]
    },
];
