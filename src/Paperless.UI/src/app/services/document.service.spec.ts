import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import {
  DocumentService,
  DataChangeEvent,
  DocumentItem,
  CategoryItem,
  CreateCategoryDto,
  UpdateCategoryDto,
} from './document.service';

describe('DocumentService', () => {
  let service: DocumentService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [DocumentService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(DocumentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  function observeChanges(): DataChangeEvent[] {
    const changes: DataChangeEvent[] = [];
    service.dataChanged$.subscribe((change) => changes.push(change));
    return changes;
  }

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  describe('Document Operations', () => {
    it('getDocuments() should send GET /api/documents and return documents', () => {
      const mockDocs: DocumentItem[] = [
        {
          id: 1,
          title: 'Bill',
          fileName: 'bill.pdf',
          contentType: 'application/pdf',
          fileSizeBytes: 1024,
          createdAt: '2026-01-01',
          hasFile: true,
        },
      ];

      service.getDocuments().subscribe((docs) => {
        expect(docs.length).toBe(1);
        expect(docs).toEqual(mockDocs);
      });

      const req = httpMock.expectOne('/api/documents');
      expect(req.request.method).toBe('GET');
      req.flush(mockDocs);
    });

    it('getDocumentById() should send GET /api/documents/:id', () => {
      const mockDoc: DocumentItem = {
        id: 5,
        title: 'Contract',
        fileName: 'doc.pdf',
        contentType: 'application/pdf',
        fileSizeBytes: 2048,
        createdAt: '2026-01-01',
        hasFile: true,
      };

      service.getDocumentById(5).subscribe((doc) => {
        expect(doc.id).toBe(5);
        expect(doc.title).toBe('Contract');
      });

      const req = httpMock.expectOne('/api/documents/5');
      expect(req.request.method).toBe('GET');
      req.flush(mockDoc);
    });

    it('uploadDocument() sends the original file and category as multipart data', () => {
      const changes = observeChanges();
      const file = new File(['bytes'], 'document.odt', {
        type: 'application/vnd.oasis.opendocument.text',
      });

      service.uploadDocument(file, 3).subscribe((res) => {
        expect(res.id).toBe(10);
      });

      const req = httpMock.expectOne('/api/documents');
      expect(req.request.method).toBe('POST');
      req.flush({
        id: 10,
        title: 'document',
        fileName: file.name,
        contentType: file.type,
        fileSizeBytes: file.size,
        createdAt: '2026-01-01',
        categoryId: 3,
        hasFile: true,
      });
      expect(req.request.body).toBeInstanceOf(FormData);
      const uploadedFile = (req.request.body as FormData).get('file') as File;
      expect(uploadedFile.name).toBe(file.name);
      expect(uploadedFile.size).toBe(file.size);
      expect((req.request.body as FormData).get('categoryId')).toBe('3');
      expect(changes).toEqual([{ entity: 'document', action: 'created' }]);
    });

    it('updateDocument() sends an optional replacement file and category', () => {
      const changes = observeChanges();
      const file = new File(['text'], 'updated.md', { type: 'text/markdown' });

      service.updateDocument(10, file, null).subscribe();

      const req = httpMock.expectOne('/api/documents/10');
      expect(req.request.method).toBe('PUT');
      const replacementFile = (req.request.body as FormData).get('file') as File;
      expect(replacementFile.name).toBe(file.name);
      expect(replacementFile.size).toBe(file.size);
      expect((req.request.body as FormData).get('categoryId')).toBe('');
      req.flush(null);
      expect(changes).toEqual([{ entity: 'document', action: 'updated' }]);
    });

    it('deleteDocument() should send DELETE /api/documents/:id', () => {
      const changes = observeChanges();
      service.deleteDocument(10).subscribe();

      const req = httpMock.expectOne('/api/documents/10');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
      expect(changes).toEqual([{ entity: 'document', action: 'deleted' }]);
    });

    it('does not notify listeners when a document mutation fails', () => {
      const changes = observeChanges();
      service.deleteDocument(10).subscribe({ error: () => undefined });

      const req = httpMock.expectOne('/api/documents/10');
      req.flush('Failed', { status: 500, statusText: 'Server Error' });

      expect(changes).toEqual([]);
    });
  });

  describe('Category Operations', () => {
    it('getCategories() should send GET /api/categories', () => {
      const mockCats: CategoryItem[] = [{ id: 1, name: 'Taxes', description: 'Tax docs' }];

      service.getCategories().subscribe((cats) => {
        expect(cats.length).toBe(1);
        expect(cats[0].name).toBe('Taxes');
      });

      const req = httpMock.expectOne('/api/categories');
      expect(req.request.method).toBe('GET');
      req.flush(mockCats);
    });

    it('createCategory() should send POST /api/categories', () => {
      const changes = observeChanges();
      const newCat: CreateCategoryDto = { name: 'Health', description: 'Medical records' };
      const createdCat: CategoryItem = { id: 2, ...newCat };

      service.createCategory(newCat).subscribe((res) => {
        expect(res.id).toBe(2);
      });

      const req = httpMock.expectOne('/api/categories');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(newCat);
      req.flush(createdCat);
      expect(changes).toEqual([{ entity: 'category', action: 'created' }]);
    });

    it('updateCategory() should send PUT /api/categories/:id', () => {
      const changes = observeChanges();
      const updateCat: UpdateCategoryDto = { name: 'Health & Care', description: 'Updated' };

      service.updateCategory(2, updateCat).subscribe();

      const req = httpMock.expectOne('/api/categories/2');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(updateCat);
      req.flush(null);
      expect(changes).toEqual([{ entity: 'category', action: 'updated' }]);
    });

    it('deleteCategory() should send DELETE /api/categories/:id', () => {
      const changes = observeChanges();
      service.deleteCategory(2).subscribe();

      const req = httpMock.expectOne('/api/categories/2');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
      expect(changes).toEqual([{ entity: 'category', action: 'deleted' }]);
    });
  });
});
