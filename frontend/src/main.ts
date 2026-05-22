import { DATE_PIPE_DEFAULT_OPTIONS, registerLocaleData } from '@angular/common';
import localeEsPe from '@angular/common/locales/es-PE';
import { bootstrapApplication } from '@angular/platform-browser';
import { APP_INITIALIZER, LOCALE_ID } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { AppComponent } from './app/app.component';
import { routes } from './app/app.routes';
import { provideQuillConfig } from 'ngx-quill';
import { authTokenInterceptor } from './app/core/interceptors/auth-token.interceptor';
import { environment } from './environments/environment';
import { AppConfigService } from './app/core/services/app-config.service';

registerLocaleData(localeEsPe);

bootstrapApplication(AppComponent, {
  providers: [
    provideRouter(routes),
    provideAnimationsAsync(),
    provideHttpClient(withInterceptors([authTokenInterceptor])),
    { provide: LOCALE_ID, useValue: environment.locale },
    { provide: DATE_PIPE_DEFAULT_OPTIONS, useValue: { timezone: environment.timeZone } },
    {
      provide: APP_INITIALIZER,
      useFactory: (cfg: AppConfigService) => () => cfg.loadConfig(),
      deps: [AppConfigService],
      multi: true,
    },
    provideQuillConfig({
      modules: {
        toolbar: [
          ['bold', 'italic', 'underline'],
          [{ 'list': 'ordered' }, { 'list': 'bullet' }],
          [{ 'header': [1, 2, 3, false] }],
          ['link', 'clean'],
        ],
      },
    }),
  ],
}).catch((err) => console.error(err));
