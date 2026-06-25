import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { ActivatedRoute } from '@angular/router';

interface ReceiptItem {
  itemName: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  taxRate: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  // === TABS & PANELS STATE ===
  leftTab: 'camera' | 'upload' = 'camera';
  showPreview: boolean = false;

  // === ZOOM STATE ===
  zoomScale: number = 1.0;
  panX: number = 0;
  panY: number = 0;
  isDragging: boolean = false;
  dragStartX: number = 0;
  dragStartY: number = 0;

  // === LEFT PANEL ===
  previewUrl: string | null = null;
  selectedFile: File | null = null;

  // === MIDDLE PANEL ===
  receiptId: number | null = null;
  merchantName: string = '';
  vknTckn: string = '';
  receiptDate: string = '';
  receiptNo: string = '';
  totalAmount: number = 0;
  taxAmount: number = 0;
  imagePath: string | null = null;
  
  statusMessage: string = '';
  statusType: 'success' | 'info' | 'error' | null = null;

  // === RIGHT PANEL: ARCHIVE ===
  receiptsList: any[] = [];
  filteredReceipts: any[] = [];
  searchQuery: string = '';
  loadingArchive: boolean = false;

  constructor(private apiService: ApiService, private cdr: ChangeDetectorRef, private route: ActivatedRoute) {}

  ngOnInit(): void {
    this.clearForm();
    this.fetchReceiptsList();
    this.route.queryParams.subscribe(params => {
      const editId = params['edit'];
      if (editId) {
        this.loadReceiptForEdit(Number(editId));
      }
    });
  }

  // === LEFT PANEL METHODS ===
  setLeftTab(tab: 'camera' | 'upload'): void {
    this.leftTab = tab;
  }

  // Mock capturing image
  captureImage(): void {
    this.showStatus('Kamera özelliği Dashboard üzerinden değil, Kamera sayfasından kullanılmalıdır.', 'info');
  }

  // File Upload OCR
  onFileSelected(event: any): void {
    const file = event.target.files[0];
    if (file) {
      this.selectedFile = file;
      
      const reader = new FileReader();
      reader.onload = (e: any) => {
        this.previewUrl = e.target.result;
        this.cdr.detectChanges();
      };
      reader.readAsDataURL(file);

      this.showStatus('Dosya yükleniyor ve Gemini OCR tarafından çözümleniyor...', 'info');
      
      // BİZİM GERÇEK .NET ENDPOINT'İMİZİ ÇAĞIRIR (/api/receipt/scan)
      this.apiService.scanReceipt(file).subscribe({
        next: (res) => {
          // Local backend response mapping
          this.merchantName = res.merchant_name || res.merchantName || '';
          this.vknTckn = res.vkn_tckn || res.vknTckn || '';
          this.receiptDate = res.receipt_date || res.receiptDate || '';
          this.receiptNo = res.receipt_no || res.receiptNo || '';
          this.totalAmount = res.total_amount || res.totalAmount || 0;
          this.taxAmount = res.tax_amount || res.taxAmount || 0;
          this.imagePath = res.image_path || res.imagePath || null;

          this.showPreview = true;
          this.showStatus('OCR tamamlandı!', 'success');
          this.cdr.detectChanges();
          setTimeout(() => {
            this.clearStatus();
            this.cdr.detectChanges();
          }, 2500);
        },
        error: (err) => {
          this.showStatus('Görüntü okunamadı: ' + err.message, 'error');
          this.cdr.detectChanges();
        }
      });
    }
  }

  resetInput(): void {
    this.previewUrl = null;
    this.selectedFile = null;
    this.showPreview = false;
    this.clearStatus();
  }

  clearForm(): void {
    this.receiptId = null;
    this.merchantName = '';
    this.vknTckn = '';
    this.receiptDate = new Date().toISOString().substring(0, 10);
    this.receiptNo = '';
    this.totalAmount = 0;
    this.taxAmount = 0;
    this.imagePath = null;
    this.resetInput();
  }

  saveReceipt(): void {
    if (!this.merchantName.trim()) {
      this.showStatus('Lütfen Mağaza Adını girin.', 'error');
      return;
    }

    const payload = {
      id: this.receiptId || 0,
      merchantName: this.merchantName,
      vknTckn: this.vknTckn,
      receiptDate: this.receiptDate,
      fisNo: this.receiptNo,
      totalAmount: this.totalAmount,
      taxAmount: this.taxAmount,
      category: 'Diğer',
      paymentMethod: 'Nakit',
      imagePath: this.imagePath,
      createdBy: localStorage.getItem('username') || 'default',
      items: []
    };

    this.showStatus('Kaydediliyor...', 'info');
    this.cdr.detectChanges();

    const req = this.receiptId 
      ? this.apiService.updateReceipt(this.receiptId, payload)
      : this.apiService.saveReceipt(payload);

    req.subscribe({
      next: (res) => {
        this.showStatus('İşlem tamamlandı!', 'success');
        this.fetchReceiptsList();

        setTimeout(() => {
          this.clearForm();
          this.cdr.detectChanges();
        }, 1200);
      },
      error: (err) => {
        this.showStatus('Kayıt başarısız oldu: ' + err.message, 'error');
        this.cdr.detectChanges();
      }
    });
  }

  cancelEdit(): void {
    this.clearForm();
  }

  downloadExcel(): void {
    const username = localStorage.getItem('username') || 'default';
    window.open(`http://localhost:5000/api/receipts/export?username=${encodeURIComponent(username)}`, '_blank');
  }

  // === ARCHIVE METHODS ===
  fetchReceiptsList(): void {
    this.loadingArchive = true;
    this.cdr.detectChanges();
    const currentUsername = localStorage.getItem('username') || '';
    this.apiService.getReceipts(currentUsername).subscribe({
      next: (data) => {
        this.receiptsList = data;
        this.filterReceipts();
        this.loadingArchive = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.loadingArchive = false;
        this.cdr.detectChanges();
      }
    });
  }

  filterReceipts(): void {
    if (!this.searchQuery.trim()) {
      this.filteredReceipts = this.receiptsList;
      return;
    }
    const q = this.searchQuery.toLowerCase();
    this.filteredReceipts = this.receiptsList.filter(r => 
      r.merchantName.toLowerCase().includes(q) ||
      r.receiptDate.toLowerCase().includes(q) ||
      (r.receiptNo && r.receiptNo.toLowerCase().includes(q)) ||
      (r.vknTckn && r.vknTckn.toLowerCase().includes(q)) ||
      r.id.toString().includes(q)
    );
  }

  loadReceiptForEdit(id: number): void {
    this.showStatus('Fatura bilgileri forma yükleniyor...', 'info');
    this.cdr.detectChanges();
    this.apiService.getReceiptDetails(id).subscribe({
      next: (data) => {
        this.receiptId = data.id;
        this.merchantName = data.merchantName;
        this.vknTckn = data.vknTckn || '';
        this.receiptDate = data.receiptDate;
        this.receiptNo = data.receiptNo || '';
        this.totalAmount = data.totalAmount;
        this.taxAmount = data.taxAmount;
        this.imagePath = data.imagePath;

        if (data.imagePath) {
          this.previewUrl = `http://localhost:5000/${data.imagePath}`;
          this.showPreview = true;
        } else {
          this.previewUrl = null;
          this.showPreview = false;
        }
        
        this.showStatus('Fatura düzenleme moduna alındı.', 'success');
        this.cdr.detectChanges();
        setTimeout(() => {
          this.clearStatus();
          this.cdr.detectChanges();
        }, 1500);
      },
      error: (err) => {
        this.showStatus('Veri okuma hatası: ' + err.message, 'error');
        this.cdr.detectChanges();
      }
    });
  }

  deleteReceipt(id: number, event: MouseEvent): void {
    event.stopPropagation();
    if (confirm('Bu fiş/fatura kaydını tamamen silmek istediğinize emin misiniz?')) {
      const username = localStorage.getItem('username') || 'default';
      this.showStatus('Kayıt siliniyor...', 'info');
      this.cdr.detectChanges();
      
      this.apiService.deleteReceipt(id, username).subscribe({
        next: (res) => {
          this.showStatus('Kayıt başarıyla silindi!', 'success');
          if (this.receiptId === id) {
            this.clearForm();
          }
          this.fetchReceiptsList();
          setTimeout(() => {
            this.clearStatus();
            this.cdr.detectChanges();
          }, 2000);
        },
        error: (err) => {
          this.showStatus('Silme hatası: ' + err.message, 'error');
          this.cdr.detectChanges();
        }
      });
    }
  }

  showStatus(msg: string, type: 'success' | 'info' | 'error'): void {
    this.statusMessage = msg;
    this.statusType = type;
  }

  clearStatus(): void {
    this.statusMessage = '';
    this.statusType = null;
  }

  resetZoom(): void {
    this.zoomScale = 1.0;
    this.panX = 0;
    this.panY = 0;
  }

  zoomIn(): void {
    this.zoomScale = Math.min(this.zoomScale + 0.25, 4.0);
  }

  zoomOut(): void {
    this.zoomScale = Math.max(this.zoomScale - 0.25, 0.5);
  }

  onImageWheel(event: WheelEvent): void {
    event.preventDefault();
    const delta = event.deltaY < 0 ? 0.15 : -0.15;
    this.zoomScale = Math.max(0.5, Math.min(this.zoomScale + delta, 4.0));
  }

  onImageDragStart(event: MouseEvent): void {
    event.preventDefault();
    this.isDragging = true;
    this.dragStartX = event.clientX - this.panX;
    this.dragStartY = event.clientY - this.panY;
  }

  onImageDrag(event: MouseEvent): void {
    if (!this.isDragging) return;
    this.panX = event.clientX - this.dragStartX;
    this.panY = event.clientY - this.dragStartY;
  }

  onImageDragEnd(event: MouseEvent): void {
    this.isDragging = false;
  }
}
