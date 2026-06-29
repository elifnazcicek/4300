import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../services/api.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit {
  mode: 'login' | 'register' | 'forgot' | 'verify' = 'login';
  
  username = '';
  password = '';
  confirmPassword = '';
  email = '';
  otpCode = '';
  rememberMe = false;

  errorMessage = '';
  successMessage = '';
  loading = false;

  constructor(private router: Router, private apiService: ApiService, private cdr: ChangeDetectorRef) {
    // If already logged in, redirect straight to dashboard
    if (localStorage.getItem('isLoggedIn') === 'true') {
      this.router.navigate(['/dashboard']);
    }
  }

  ngOnInit(): void {
    // Check if "Remember Me" credentials exist
    const savedRememberMe = localStorage.getItem('rememberMe') === 'true';
    if (savedRememberMe) {
      this.rememberMe = true;
      this.username = localStorage.getItem('rememberedUsername') || '';
      this.password = localStorage.getItem('rememberedPassword') || '';
    }
  }

  switchMode(newMode: 'login' | 'register' | 'forgot' | 'verify'): void {
    this.mode = newMode;
    this.errorMessage = '';
    this.successMessage = '';
    this.password = '';
    this.confirmPassword = '';
    this.otpCode = '';
    
    if (newMode === 'login') {
      const savedRememberMe = localStorage.getItem('rememberMe') === 'true';
      if (savedRememberMe) {
        this.rememberMe = true;
        this.username = localStorage.getItem('rememberedUsername') || '';
        this.password = localStorage.getItem('rememberedPassword') || '';
      }
    }
    this.cdr.detectChanges();
  }

  toggleMode(): void {
    this.switchMode(this.mode === 'login' ? 'register' : 'login');
  }

  onSubmit(): void {
    if (this.mode === 'login') {
      this.handleLogin();
    } else if (this.mode === 'register') {
      this.handleRegister();
    } else if (this.mode === 'forgot') {
      this.handleForgot();
    } else if (this.mode === 'verify') {
      this.handleVerify();
    }
  }

  private handleLogin(): void {
    if (!this.username.trim() || !this.password.trim()) {
      this.errorMessage = 'Lütfen kullanıcı adı ve şifre giriniz.';
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.cdr.detectChanges();

    this.apiService.login({ username: this.username, password: this.password }).subscribe({
      next: (res) => {
        this.loading = false;
        if (res.success) {
          localStorage.setItem('isLoggedIn', 'true');
          localStorage.setItem('username', res.username);
          localStorage.setItem('token', res.token);

          // Save or clear Remember Me credentials
          if (this.rememberMe) {
            localStorage.setItem('rememberMe', 'true');
            localStorage.setItem('rememberedUsername', this.username);
            localStorage.setItem('rememberedPassword', this.password);
          } else {
            localStorage.removeItem('rememberMe');
            localStorage.removeItem('rememberedUsername');
            localStorage.removeItem('rememberedPassword');
          }

          this.router.navigate(['/dashboard']);
        } else {
          this.errorMessage = res.error || 'Giriş başarısız.';
        }
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.error || 'Hatalı kullanıcı adı veya şifre.';
        this.cdr.detectChanges();
      }
    });
  }

  private handleRegister(): void {
    if (!this.username.trim() || !this.password.trim() || !this.confirmPassword.trim() || !this.email.trim()) {
      this.errorMessage = 'Lütfen tüm alanları doldurun.';
      return;
    }

    if (this.password !== this.confirmPassword) {
      this.errorMessage = 'Şifreler eşleşmiyor.';
      return;
    }

    if (this.password.length < 3) {
      this.errorMessage = 'Şifre en az 3 karakter olmalıdır.';
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.cdr.detectChanges();

    this.apiService.register({ username: this.username, password: this.password, email: this.email }).subscribe({
      next: (res) => {
        this.loading = false;
        if (res.success) {
          this.successMessage = 'Profil başarıyla oluşturuldu! Giriş ekranına yönlendiriliyorsunuz...';
          this.cdr.detectChanges();

          setTimeout(() => {
            const tempUsername = this.username;
            const tempPassword = this.password;
            this.switchMode('login');
            // Auto-populate for convenience after registration
            this.username = tempUsername;
            this.password = tempPassword;
            this.cdr.detectChanges();
          }, 2000);
        } else {
          this.errorMessage = res.error || 'Kayıt başarısız.';
          this.cdr.detectChanges();
        }
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.error || 'Bu kullanıcı adı zaten alınmış olabilir.';
        this.cdr.detectChanges();
      }
    });
  }

  private handleForgot(): void {
    if (!this.username.trim() || !this.email.trim()) {
      this.errorMessage = 'Lütfen kullanıcı adı ve e-posta adresinizi giriniz.';
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.cdr.detectChanges();

    this.apiService.requestPasswordReset({ username: this.username, email: this.email }).subscribe({
      next: (res) => {
        this.loading = false;
        if (res.success) {
          this.successMessage = 'Şifre sıfırlama kodunuz e-posta adresinize gönderildi.';
          this.cdr.detectChanges();
          setTimeout(() => {
            this.switchMode('verify');
          }, 1500);
        } else {
          this.errorMessage = res.error || 'Kod gönderme işlemi başarısız.';
        }
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.error || 'Doğrulama kodu gönderilemedi. Bilgilerinizi kontrol edin.';
        this.cdr.detectChanges();
      }
    });
  }

  private handleVerify(): void {
    if (!this.username.trim() || !this.otpCode.trim() || !this.password.trim() || !this.confirmPassword.trim()) {
      this.errorMessage = 'Lütfen tüm alanları doldurun.';
      return;
    }

    if (this.password !== this.confirmPassword) {
      this.errorMessage = 'Şifreler eşleşmiyor.';
      return;
    }

    if (this.password.length < 3) {
      this.errorMessage = 'Şifre en az 3 karakter olmalıdır.';
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.cdr.detectChanges();

    this.apiService.verifyPasswordReset({
      username: this.username,
      otpCode: this.otpCode,
      newPassword: this.password
    }).subscribe({
      next: (res) => {
        this.loading = false;
        if (res.success) {
          this.successMessage = 'Şifreniz başarıyla güncellendi! Giriş yapılıyor...';
          localStorage.setItem('isLoggedIn', 'true');
          localStorage.setItem('username', res.username);
          localStorage.setItem('token', res.token);
          this.cdr.detectChanges();
          
          setTimeout(() => {
            this.router.navigate(['/dashboard']);
          }, 1500);
        } else {
          this.errorMessage = res.error || 'Şifre güncelleme başarısız.';
        }
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.loading = false;
        this.errorMessage = err.error?.error || 'Geçersiz kod veya işlem hatası.';
        this.cdr.detectChanges();
      }
    });
  }
}
