import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { OAuthAuthService } from './oauth-auth.service';
import { AuthHubService } from '../auth-hub.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const oauthToken = inject(OAuthAuthService).accessToken;
  const hubToken = inject(AuthHubService).getStoredSession()?.accessToken;
  
  const token = oauthToken || hubToken;
  
  if (!token) {
    return next(request);
  }

  return next(request.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`
    }
  }));
};
