import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject, tap } from 'rxjs';

export interface DocumentItem {
  id: number;
  title: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  createdAt: string;
  categoryId?: number | null;
  categoryName?: string;
  hasFile: boolean;
}

export interface CategoryItem {
  id: number;
  name: string;
  description?: string;
}

export const MAX_DOCUMENT_SIZE_BYTES = 25 * 1024 * 1024;

export interface CreateCategoryDto {
  name: string;
  description?: string;
}

export interface UpdateCategoryDto {
  name: string;
  description?: string;
}

export interface DataChangeEvent {
  entity: 'document' | 'category';
  action: 'created' | 'updated' | 'deleted';
}

@Injectable({
  providedIn: 'root',
})
export class DocumentService {
  private readonly docsUrl = '/api/documents';
  private readonly catsUrl = '/api/categories';
  private readonly dataChangedSubject = new Subject<DataChangeEvent>();
  readonly dataChanged$ = this.dataChangedSubject.asObservable();

  constructor(private http: HttpClient) {}

  // Documents
  getDocuments(): Observable<DocumentItem[]> {
    return this.http.get<DocumentItem[]>(this.docsUrl);
  }

  getDocumentById(id: number): Observable<DocumentItem> {
    return this.http.get<DocumentItem>(`${this.docsUrl}/${id}`);
  }

  uploadDocument(file: File, categoryId: number | null): Observable<DocumentItem> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    if (categoryId !== null) {
      formData.append('categoryId', String(categoryId));
    }
    return this.http
      .post<DocumentItem>(this.docsUrl, formData)
      .pipe(tap(() => this.notifyDataChanged({ entity: 'document', action: 'created' })));
  }

  updateDocument(id: number, file: File | null, categoryId: number | null): Observable<void> {
    const formData = new FormData();
    if (file) {
      formData.append('file', file, file.name);
    }
    formData.append('categoryId', categoryId === null ? '' : String(categoryId));
    return this.http
      .put<void>(`${this.docsUrl}/${id}`, formData)
      .pipe(tap(() => this.notifyDataChanged({ entity: 'document', action: 'updated' })));
  }

  deleteDocument(id: number): Observable<void> {
    return this.http
      .delete<void>(`${this.docsUrl}/${id}`)
      .pipe(tap(() => this.notifyDataChanged({ entity: 'document', action: 'deleted' })));
  }

  // Categories
  getCategories(): Observable<CategoryItem[]> {
    return this.http.get<CategoryItem[]>(this.catsUrl);
  }

  createCategory(cat: CreateCategoryDto): Observable<CategoryItem> {
    return this.http
      .post<CategoryItem>(this.catsUrl, cat)
      .pipe(tap(() => this.notifyDataChanged({ entity: 'category', action: 'created' })));
  }

  updateCategory(id: number, cat: UpdateCategoryDto): Observable<void> {
    return this.http
      .put<void>(`${this.catsUrl}/${id}`, cat)
      .pipe(tap(() => this.notifyDataChanged({ entity: 'category', action: 'updated' })));
  }

  deleteCategory(id: number): Observable<void> {
    return this.http
      .delete<void>(`${this.catsUrl}/${id}`)
      .pipe(tap(() => this.notifyDataChanged({ entity: 'category', action: 'deleted' })));
  }

  private notifyDataChanged(event: DataChangeEvent): void {
    this.dataChangedSubject.next(event);
  }
}
