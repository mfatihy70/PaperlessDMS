import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import {
  DocumentService,
  DocumentItem,
  CategoryItem,
  CreateDocumentDto,
  UpdateDocumentDto,
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
      };

      service.getDocumentById(5).subscribe((doc) => {
        expect(doc.id).toBe(5);
        expect(doc.title).toBe('Contract');
      });

      const req = httpMock.expectOne('/api/documents/5');
      expect(req.request.method).toBe('GET');
      req.flush(mockDoc);
    });

    it('createDocument() should send POST /api/documents with payload', () => {
      const newDoc: CreateDocumentDto = {
        title: 'New Doc',
        fileName: 'new.pdf',
        fileSizeBytes: 500,
        categoryId: null,
      };
      const responseDoc: DocumentItem = {
        id: 10,
        ...newDoc,
        contentType: 'application/pdf',
        createdAt: '2026-01-01',
      };

      service.createDocument(newDoc).subscribe((res) => {
        expect(res.id).toBe(10);
      });

      const req = httpMock.expectOne('/api/documents');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(newDoc);
      req.flush(responseDoc);
    });

    it('updateDocument() should send PUT /api/documents/:id', () => {
      const updatePayload: UpdateDocumentDto = { title: 'Updated Title', categoryId: 2 };

      service.updateDocument(10, updatePayload).subscribe();

      const req = httpMock.expectOne('/api/documents/10');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(updatePayload);
      req.flush(null);
    });

    it('deleteDocument() should send DELETE /api/documents/:id', () => {
      service.deleteDocument(10).subscribe();

      const req = httpMock.expectOne('/api/documents/10');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
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
      const newCat: CreateCategoryDto = { name: 'Health', description: 'Medical records' };
      const createdCat: CategoryItem = { id: 2, ...newCat };

      service.createCategory(newCat).subscribe((res) => {
        expect(res.id).toBe(2);
      });

      const req = httpMock.expectOne('/api/categories');
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual(newCat);
      req.flush(createdCat);
    });

    it('updateCategory() should send PUT /api/categories/:id', () => {
      const updateCat: UpdateCategoryDto = { name: 'Health & Care', description: 'Updated' };

      service.updateCategory(2, updateCat).subscribe();

      const req = httpMock.expectOne('/api/categories/2');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(updateCat);
      req.flush(null);
    });

    it('deleteCategory() should send DELETE /api/categories/:id', () => {
      service.deleteCategory(2).subscribe();

      const req = httpMock.expectOne('/api/categories/2');
      expect(req.request.method).toBe('DELETE');
      req.flush(null);
    });
  });
});
