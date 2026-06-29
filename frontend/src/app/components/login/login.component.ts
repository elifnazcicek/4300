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
  mode: 'login' | 'register' = 'login';
  
  username = '';
  password = '';
  confirmPassword = '';
  rememberMe = false;

  errorMessage = '';
  successMessage = '';
  loading = false;

  // Password reset modal state
  showResetModal = false;
  resetStep: 1 | 2 = 1;
  resetEmailOrUsername = '';
  resetCode = '';
  resetNewPassword = '';
  resetConfirmPassword = '';
  resetErrorMessage = '';
  resetSuccessMessage = '';
  resetLoading = false;


  constructor(private router: Router, private apiService: ApiService, private cdr: ChangeDetectorRef) {
    // If already logged in, redirect straight to dashboard
    if (localStorage.getItem('isLoggedIn') === 'true') {
      this.router.navigate(['/dashboard']);
    }
  }

  ngOnInit(): void {
    // Initialize default users in localStorage if empty
    let users = JSON.parse(localStorage.getItem('users') || '[]');
    if (users.length === 0) {
      users.push({ username: 'admin', password: '123' });
      localStorage.setItem('users', JSON.stringify(users));
    }

    // Check if "Remember Me" credentials exist
    const savedRememberMe = localStorage.getItem('rememberMe') === 'true';
    if (savedRememberMe) {
      this.rememberMe = true;
      this.username = localStorage.getItem('rememberedUsername') || '';
      this.password = localStorage.getItem('rememberedPassword') || '';
    }
  }

  toggleMode(): void {
    this.mode = this.mode === 'login' ? 'register' : 'login';
    this.errorMessage = '';
    this.successMessage = '';
    this.password = '';
    this.confirmPassword = '';
    
    if (this.mode === 'register') {
      this.username = '';
    } else {
      // Restore remembered credentials if switching back to login
      const savedRememberMe = localStorage.getItem('rememberMe') === 'true';
      if (savedRememberMe) {
        this.rememberMe = true;
        this.username = localStorage.getItem('rememberedUsername') || '';
        this.password = localStorage.getItem('rememberedPassword') || '';
      }
    }
  }

  onSubmit(): void {
    if (this.mode === 'login') {
      this.handleLogin();
    } else {
      this.handleRegister();
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
          localStorage.setItem('role', res.role);

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
    if (!this.username.trim() || !this.password.trim() || !this.confirmPassword.trim()) {
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

    this.apiService.register({ username: this.username, password: this.password }).subscribe({
      next: (res) => {
        this.loading = false;
        if (res.success) {
          this.successMessage = 'Profil başarıyla oluşturuldu! Giriş ekranına yönlendiriliyorsunuz...';
          this.cdr.detectChanges();

          setTimeout(() => {
            const tempUsername = this.username;
            const tempPassword = this.password;
            this.toggleMode();
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

  openResetModal(): void {
    this.showResetModal = true;
    this.resetStep = 1;
    this.resetEmailOrUsername = '';
    this.resetCode = '';
    this.resetNewPassword = '';
    this.resetConfirmPassword = '';
    this.resetErrorMessage = '';
    this.resetSuccessMessage = '';
    this.cdr.detectChanges();
  }

  closeResetModal(): void {
    this.showResetModal = false;
    this.cdr.detectChanges();
  }

  sendResetCode(): void {
    if (!this.resetEmailOrUsername.trim()) {
      this.resetErrorMessage = 'Lütfen kullanıcı adı veya e-posta giriniz.';
      return;
    }

    this.resetLoading = true;
    this.resetErrorMessage = '';
    this.resetSuccessMessage = '';
    this.cdr.detectChanges();

    this.apiService.forgotPassword(this.resetEmailOrUsername.trim()).subscribe({
      next: (res) => {
        this.resetLoading = false;
        this.resetSuccessMessage = res.message || 'Doğrulama kodu e-postanıza gönderildi.';
        this.resetStep = 2;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.resetLoading = false;
        this.resetErrorMessage = err.error?.message || 'E-posta gönderilirken bir hata oluştu.';
        this.cdr.detectChanges();
      }
    });
  }

  verifyResetCodeAndSetPassword(): void {
    if (!this.resetCode.trim() || !this.resetNewPassword.trim() || !this.resetConfirmPassword.trim()) {
      this.resetErrorMessage = 'Lütfen tüm alanları doldurun.';
      return;
    }

    if (this.resetNewPassword !== this.resetConfirmPassword) {
      this.resetErrorMessage = 'Şifreler eşleşmiyor.';
      return;
    }

    if (this.resetNewPassword.length < 3) {
      this.resetErrorMessage = 'Yeni şifre en az 3 karakter olmalıdır.';
      return;
    }

    this.resetLoading = true;
    this.resetErrorMessage = '';
    this.resetSuccessMessage = '';
    this.cdr.detectChanges();

    const payload = {
      emailOrUsername: this.resetEmailOrUsername.trim(),
      code: this.resetCode.trim(),
      newPassword: this.resetNewPassword
    };

    this.apiService.resetPassword(payload).subscribe({
      next: (res) => {
        this.resetLoading = false;
        this.resetSuccessMessage = res.message || 'Şifreniz başarıyla sıfırlandı. Giriş yapabilirsiniz.';
        this.cdr.detectChanges();

        setTimeout(() => {
          this.closeResetModal();
          this.username = this.resetEmailOrUsername;
          this.password = '';
          this.cdr.detectChanges();
        }, 2000);
      },
      error: (err) => {
        this.resetLoading = false;
        this.resetErrorMessage = err.error?.message || 'Şifre sıfırlanamadı. Lütfen doğrulama kodunu kontrol edin.';
        this.cdr.detectChanges();
      }
    });
  }
}
