import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { apiErrorInterceptor, coldStartInterceptor, demoAuthInterceptor } from './core/interceptors';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    // Order matters: identity is attached before the request leaves; errors are normalised on the way back.
    provideHttpClient(withInterceptors([coldStartInterceptor, demoAuthInterceptor, apiErrorInterceptor]))
  ]
};
