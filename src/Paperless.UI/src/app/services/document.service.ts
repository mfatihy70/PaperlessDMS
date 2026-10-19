import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface DocumentItem {
  id: number;
  title: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  createdAt: string;
  categoryId?: number | null;
  categoryName?: string;
}

export interface CategoryItem {
  id: number;
  name: string;
  description?: string;
}

export interface CreateDocumentDto {
  title: string;
  fileName: string;
  contentType?: string;
  fileSizeBytes: number;
  categoryId?: number | null;
}

export interface UpdateDocumentDto {
  title: string;
  categoryId?: number | null;
}

export interface CreateCategoryDto {
  name: string;
  description?: string;
}

@Injectable({
  providedIn: 'root'
})
export class DocumentService {
  private readonly docsUrl = '/api/documents';
  private readonly catsUrl = '/api/categories';

  constructor(private http: HttpClient) {}

  // --- Document Operations ---
  getDocuments(): Observable<DocumentItem[]> {
    return this.http.get<DocumentItem[]>(this.docsUrl);
  }

  createDocument(doc: CreateDocumentDto): Observable<DocumentItem> {
    return this.http.post<DocumentItem>(this.docsUrl, doc);
  }

  updateDocument(id: number, doc: UpdateDocumentDto): Observable<void> {
    return this.http.put<void>(`${this.docsUrl}/${id}`, doc);
  }

  deleteDocument(id: number): Observable<void> {
    return this.http.delete<void>(`${this.docsUrl}/${id}`);
  }

  // --- Category Operations ---
  getCategories(): Observable<CategoryItem[]> {
    return this.http.get<CategoryItem[]>(this.catsUrl);
  }

  createCategory(cat: CreateCategoryDto): Observable<CategoryItem> {
    return this.http.post<CategoryItem>(this.catsUrl, cat);
  }
}