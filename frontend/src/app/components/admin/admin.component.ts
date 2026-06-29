import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin.component.html',
  styleUrls: ['./admin.component.css']
})
export class AdminComponent implements OnInit {
  users: any[] = [];
  loadingUsers: boolean = false;
  currentUsername: string = '';

  statusMessage: string = '';
  statusType: 'success' | 'error' | null = null;

  constructor(
    private apiService: ApiService, 
    private cdr: ChangeDetectorRef,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.currentUsername = localStorage.getItem('username') || '';
    this.fetchUsers();
  }

  isMasterAdmin(): boolean {
    return this.currentUsername.toLowerCase() === 'stajyer';
  }

  fetchUsers(): void {
    this.loadingUsers = true;
    this.cdr.detectChanges();

    this.apiService.getUsers().subscribe({
      next: (data) => {
        this.users = data;
        this.loadingUsers = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.showStatus('Kullanıcı listesi alınırken hata oluştu.', 'error');
        this.loadingUsers = false;
        this.cdr.detectChanges();
      }
    });
  }

  toggleUserRole(user: any): void {
    const newRole = user.role === 'Admin' ? 'User' : 'Admin';
    
    // Prevent self-demotion check on UI
    if (user.username.toLowerCase() === 'stajyer') {
      this.showStatus('Ana stajyer yöneticisinin yetkisini geri alamazsınız.', 'error');
      return;
    }

    this.apiService.updateUserRole(user.id, newRole).subscribe({
      next: (res) => {
        user.role = newRole;
        this.showStatus(`'${user.username}' kullanıcısı '${newRole}' rolüne güncellendi.`, 'success');
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.showStatus(err.error?.message || 'Yetki güncellenirken hata oluştu.', 'error');
        this.cdr.detectChanges();
      }
    });
  }

  toggleUserActive(user: any): void {
    const newStatus = !user.isActive;

    // Prevent disabling stajyer
    if (user.username.toLowerCase() === 'stajyer' && !newStatus) {
      this.showStatus('Ana stajyer yöneticisi pasif hale getirilemez.', 'error');
      return;
    }

    this.apiService.updateUserStatus(user.id, newStatus).subscribe({
      next: (res) => {
        user.isActive = newStatus;
        this.showStatus(`'${user.username}' kullanıcısı durum bilgisi güncellendi.`, 'success');
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.showStatus(err.error?.message || 'Durum güncellenirken hata oluştu.', 'error');
        this.cdr.detectChanges();
      }
    });
  }

  showStatus(msg: string, type: 'success' | 'error'): void {
    this.statusMessage = msg;
    this.statusType = type;
    this.cdr.detectChanges();

    setTimeout(() => {
      this.statusMessage = '';
      this.statusType = null;
      this.cdr.detectChanges();
    }, 4000);
  }
}
