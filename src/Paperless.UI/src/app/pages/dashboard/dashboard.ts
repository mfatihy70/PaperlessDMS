import { Component, OnInit, signal, viewChild } from '@angular/core';
import { forkJoin } from 'rxjs';
import {
  DocumentService,
  DocumentItem,
  CategoryItem,
  CreateDocumentDto,
  CreateCategoryDto,
  UpdateCategoryDto,
} from '../../services/document.service';
import { DocumentForm } from '../../components/document-form/document-form';
import { CategoryForm } from '../../components/category-form/category-form';
import { DocumentTable } from '../../components/document-table/document-table';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DocumentForm, CategoryForm, DocumentTable],
  templateUrl: './dashboard.html',
  styleUrls: ['./dashboard.scss'],
})
export class Dashboard implements OnInit {
  docFormRef = viewChild<DocumentForm>(DocumentForm);
  catFormRef = viewChild<CategoryForm>(CategoryForm);

  documents = signal<DocumentItem[]>([]);
  categories = signal<CategoryItem[]>([]);
  filteredDocuments = signal<DocumentItem[]>([]);

  searchTerm = signal<string>('');
  selectedCatId = signal<number | null>(null);

  isLoading = signal<boolean>(false);
  isSavingDoc = signal<boolean>(false);
  isSavingCat = signal<boolean>(false);

  errorMessage = signal<string>('');
  successMessage = signal<string>('');

  constructor(private api: DocumentService) {}

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    forkJoin({
      categories: this.api.getCategories(),
      documents: this.api.getDocuments(),
    }).subscribe({
      next: (res) => {
        this.categories.set(res.categories);
        this.documents.set(res.documents);
        this.applyFilter();
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set('Backend service starting up or unreachable. Click refresh.');
        this.isLoading.set(false);
      },
    });
  }

  applyFilter(): void {
    const term = this.searchTerm().toLowerCase();
    const catId = this.selectedCatId();

    const filtered = this.documents().filter((doc) => {
      const matchesTerm =
        doc.title.toLowerCase().includes(term) || doc.fileName.toLowerCase().includes(term);
      const matchesCat = catId === null || doc.categoryId === catId;
      return matchesTerm && matchesCat;
    });
    this.filteredDocuments.set(filtered);
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.applyFilter();
  }

  onFilterCategory(id: number | null): void {
    this.selectedCatId.set(id);
    this.applyFilter();
  }

  handleUpload(dto: CreateDocumentDto): void {
    this.isSavingDoc.set(true);
    this.api.createDocument(dto).subscribe({
      next: () => {
        this.showSuccess('Document uploaded successfully!');
        this.docFormRef()?.reset();
        this.isSavingDoc.set(false);
        this.loadData();
      },
      error: (err) => {
        this.errorMessage.set('Upload failed: ' + err.message);
        this.isSavingDoc.set(false);
      },
    });
  }

  handleCreateCategory(dto: CreateCategoryDto): void {
    this.isSavingCat.set(true);
    this.api.createCategory(dto).subscribe({
      next: () => {
        this.showSuccess('Category created!');
        this.catFormRef()?.reset();
        this.isSavingCat.set(false);
        this.loadData();
      },
      error: (err) => {
        this.errorMessage.set('Category creation failed: ' + err.message);
        this.isSavingCat.set(false);
      },
    });
  }

  handleEditCategory(event: { id: number; data: UpdateCategoryDto }): void {
    this.api.updateCategory(event.id, event.data).subscribe({
      next: () => {
        this.showSuccess('Category updated!');
        this.loadData();
      },
      error: (err) => this.errorMessage.set('Category update failed: ' + err.message),
    });
  }

  handleDeleteCategory(id: number): void {
    this.api.deleteCategory(id).subscribe({
      next: () => {
        if (this.selectedCatId() === id) {
          this.selectedCatId.set(null);
        }
        this.showSuccess('Category deleted.');
        this.loadData();
      },
      error: (err) => this.errorMessage.set('Category deletion failed: ' + err.message),
    });
  }

  handleDeleteDocument(id: number): void {
    this.api.deleteDocument(id).subscribe({
      next: () => {
        this.showSuccess('Document deleted.');
        this.loadData();
      },
      error: (err) => this.errorMessage.set('Delete failed: ' + err.message),
    });
  }

  private showSuccess(msg: string): void {
    this.successMessage.set(msg);
    setTimeout(() => this.successMessage.set(''), 3500);
  }
}
