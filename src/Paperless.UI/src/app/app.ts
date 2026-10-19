import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { 
  DocumentService, 
  DocumentItem, 
  CategoryItem, 
  CreateDocumentDto, 
  CreateCategoryDto, 
  UpdateDocumentDto 
} from './services/document.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrls: ['./app.scss']
})
export class App implements OnInit {
  documents: DocumentItem[] = [];
  categories: CategoryItem[] = [];
  errorMessage: string = '';

  // Forms
  newDoc: CreateDocumentDto = {
    title: '',
    fileName: '',
    contentType: 'application/pdf',
    fileSizeBytes: 1024,
    categoryId: null
  };

  newCat: CreateCategoryDto = {
    name: '',
    description: ''
  };

  // Editing state
  editingDocId: number | null = null;
  editDocPayload: UpdateDocumentDto = {
    title: '',
    categoryId: null
  };

  constructor(private api: DocumentService) {}

  ngOnInit(): void {
    this.refreshAll();
  }

  refreshAll(): void {
    this.errorMessage = '';
    this.api.getCategories().subscribe({
      next: (data) => (this.categories = data),
      error: (err) => (this.errorMessage = 'Failed to load categories: ' + (err.message || 'Server error'))
    });

    this.api.getDocuments().subscribe({
      next: (data) => (this.documents = data),
      error: (err) => (this.errorMessage = 'Failed to load documents: ' + (err.message || 'Server error'))
    });
  }

  createCategory(): void {
    if (!this.newCat.name.trim()) return;
    this.api.createCategory(this.newCat).subscribe({
      next: () => {
        this.newCat = { name: '', description: '' };
        this.refreshAll();
      },
      error: (err) => (this.errorMessage = 'Category creation failed: ' + err.message)
    });
  }

  createDocument(): void {
    if (!this.newDoc.title.trim() || !this.newDoc.fileName.trim()) return;
    this.api.createDocument(this.newDoc).subscribe({
      next: () => {
        this.newDoc = {
          title: '',
          fileName: '',
          contentType: 'application/pdf',
          fileSizeBytes: 1024,
          categoryId: null
        };
        this.refreshAll();
      },
      error: (err) => (this.errorMessage = 'Document creation failed: ' + err.message)
    });
  }

  startEdit(doc: DocumentItem): void {
    this.editingDocId = doc.id;
    this.editDocPayload = {
      title: doc.title,
      categoryId: doc.categoryId ?? null
    };
  }

  cancelEdit(): void {
    this.editingDocId = null;
  }

  saveEdit(id: number): void {
    this.api.updateDocument(id, this.editDocPayload).subscribe({
      next: () => {
        this.editingDocId = null;
        this.refreshAll();
      },
      error: (err) => (this.errorMessage = 'Update failed: ' + err.message)
    });
  }

  deleteDocument(id: number): void {
    if (!confirm('Are you sure you want to delete this document?')) return;
    this.api.deleteDocument(id).subscribe({
      next: () => this.refreshAll(),
      error: (err) => (this.errorMessage = 'Delete failed: ' + err.message)
    });
  }
}