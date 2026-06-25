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
  isDragActive: boolean = false;

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
      this.processImageFile(file);
    }
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragActive = true;
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragActive = false;
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragActive = false;

    const dt = event.dataTransfer;
    if (!dt) return;

    // 1. Dosya sürüklenip bırakıldıysa (Local File Explorer)
    if (dt.files && dt.files.length > 0) {
      const file = dt.files[0];
      if (file.type.startsWith('image/')) {
        this.processImageFile(file);
      } else {
        this.showStatus('Lütfen sadece görsel dosyası yükleyin.', 'error');
      }
      return;
    }

    // 2. Başka bir sekmeden (örn: WhatsApp Web) görsel sürüklenip bırakıldıysa
    const html = dt.getData('text/html');
    const urlList = dt.getData('text/uri-list');
    const plainText = dt.getData('text/plain');

    let imageUrl = '';

    if (html) {
      const match = html.match(/<img[^>]+src="([^">]+)"/);
      if (match && match[1]) {
        imageUrl = match[1];
      }
    }

    if (!imageUrl && urlList) {
      imageUrl = urlList.split('\n')[0].trim();
    }

    if (!imageUrl && plainText && (plainText.startsWith('http') || plainText.startsWith('data:image/'))) {
      imageUrl = plainText.trim();
    }

    if (imageUrl) {
      this.processImageUrl(imageUrl);
    } else {
      this.showStatus('Sürüklenen veri görsel olarak çözümlenemedi.', 'error');
    }
  }

  processImageFile(file: File): void {
    this.selectedFile = file;
    
    const reader = new FileReader();
    reader.onload = (e: any) => {
      this.previewUrl = e.target.result;
      this.cdr.detectChanges();
    };
    reader.readAsDataURL(file);

    this.showStatus('Dosya yükleniyor ve Gemini OCR tarafından çözümleniyor...', 'info');
    this.cdr.detectChanges();

    this.apiService.scanReceipt(file).subscribe({
      next: (res) => {
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

  processImageUrl(url: string): void {
    this.showStatus('Sürüklenen görsel işleniyor...', 'info');
    this.cdr.detectChanges();

    // Base64 veri URL'si ise
    if (url.startsWith('data:image/')) {
      try {
        const blob = this.dataURLtoBlob(url);
        const file = new File([blob], 'dragged_image.jpg', { type: blob.type });
        this.processImageFile(file);
      } catch (e) {
        this.showStatus('Base64 görsel çözümlenirken hata oluştu.', 'error');
      }
      return;
    }

    // Blob URL'i veya HTTP URL'i ise fetch etmeyi dene
    fetch(url)
      .then(res => res.blob())
      .then(blob => {
        const file = new File([blob], 'dragged_image.jpg', { type: blob.type || 'image/jpeg' });
        this.processImageFile(file);
      })
      .catch(err => {
        this.showStatus('Görsel doğrudan indirilemedi (CORS engeli). Lütfen resmi bilgisayarınıza kaydedip sürükleyin.', 'error');
        this.cdr.detectChanges();
      });
  }

  dataURLtoBlob(dataurl: string): Blob {
    const arr = dataurl.split(',');
    const mimeMatch = arr[0].match(/:(.*?);/);
    const mime = mimeMatch ? mimeMatch[1] : 'image/jpeg';
    const bstr = atob(arr[1]);
    let n = bstr.length;
    const u8arr = new Uint8Array(n);
    while (n--) {
      u8arr[n] = bstr.charCodeAt(n);
    }
    return new Blob([u8arr], { type: mime });
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
      this.filteredReceipts = this.receiptsList.slice(0, 10);
      return;
    }
    const q = this.searchQuery.toLowerCase();
    this.filteredReceipts = this.receiptsList
      .filter(r => 
        r.merchantName.toLowerCase().includes(q) ||
        r.receiptDate.toLowerCase().includes(q) ||
        (r.receiptNo && r.receiptNo.toLowerCase().includes(q)) ||
        (r.vknTckn && r.vknTckn.toLowerCase().includes(q)) ||
        r.id.toString().includes(q)
      )
      .slice(0, 10);
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
