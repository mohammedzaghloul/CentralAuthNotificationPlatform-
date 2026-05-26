import { Component } from '@angular/core';
import { OAuthAuthService } from '../auth/oauth-auth.service';

@Component({
  selector: 'auth-login',
  standalone: true,
  imports: [],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  currentYear = new Date().getFullYear();
  
  constructor(private readonly auth: OAuthAuthService) {}

  redirectToLogin(): void {
    void this.auth.redirectToLogin();
  }
}
