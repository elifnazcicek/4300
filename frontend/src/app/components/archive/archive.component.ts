import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-archive',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './archive.component.html',
  styleUrls: ['./archive.component.css']
})
export class ArchiveComponent implements OnInit {
  receipts: any[] = [];
  filteredReceipts: any[] = [];
  searchQuery: string = '';
  loading: boolean = false;
  errorMessage: string = '';

  constructor(private apiService: ApiService, private router: Router, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.fetchReceipts();
  }

  fetchReceipts(): void {
    this.loading = true;
    this.errorMessage = '';
    this.cdr.detectChanges();
    
    this.apiService.getReceipts().subscribe({
      next: (data) => {
        this.receipts = data;
        this.filterReceipts();
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.errorMessage = 'Veriler yüklenirken hata oluştu: ' + err.message;
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  filterReceipts(): void {
    if (!this.searchQuery.trim()) {
      this.filteredReceipts = this.receipts;
      return;
    }

    const query = this.searchQuery.toLowerCase();
    this.filteredReceipts = this.receipts.filter(r => 
      r.merchantName.toLowerCase().includes(query) ||
      r.receiptDate.toLowerCase().includes(query) ||
      r.id.toString().includes(query)
    );
  }

  editReceipt(id: number): void {
    this.router.navigate(['/dashboard'], { queryParams: { edit: id } });
  }

  deleteReceipt(id: number): void {
    if (confirm('Bu fiş/fatura kaydını tamamen silmek istediğinize emin misiniz?')) {
      const username = localStorage.getItem('username') || 'default';
      this.apiService.deleteReceipt(id, username).subscribe({
        next: (res) => {
          this.fetchReceipts();
        },
        error: (err) => {
          alert('Silme hatası: ' + err.message);
        }
      });
    }
  }

  downloadExcel(): void {
    window.open('http://localhost:5000/api/receipts/export', '_blank');
  }
}
