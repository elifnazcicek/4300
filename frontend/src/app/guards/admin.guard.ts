import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

export const adminGuard: CanActivateFn = (route, state) => {
  const router = inject(Router);
  const isLoggedIn = localStorage.getItem('isLoggedIn') === 'true';
  const role = localStorage.getItem('role');

  if (!isLoggedIn) {
    router.navigate(['/login']);
    return false;
  }

  // Sadece Admin'in erişebileceği sayfalar (örn: /admin)
  if (state.url.includes('/admin')) {
    if (role === 'Admin') {
      return true;
    } else {
      router.navigate(['/dashboard']);
      return false;
    }
  }

  // Logs sayfası: Standart User erişemez
  if (state.url.includes('/logs')) {
    if (role === 'Admin' || role === 'Auditor') {
      return true;
    } else {
      router.navigate(['/dashboard']);
      return false;
    }
  }

  return true;
};
