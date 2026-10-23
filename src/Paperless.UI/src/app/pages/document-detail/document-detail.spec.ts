import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router, ActivatedRoute } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { of, throwError } from 'rxjs';
import { DocumentDetail } from './document-detail';
import { DocumentService, DocumentItem, CategoryItem } from '../../services/document.service';

describe('DocumentDetail Page', () => {
  let fixture: ComponentFixture<DocumentDetail>;
  let component: DocumentDetail;
  let docServiceSpy: {
    getDocumentById: ReturnType<typeof vi.fn>;
    getCategories: ReturnType<typeof vi.fn>;
    updateDocument: ReturnType<typeof vi.fn>;
    deleteDocument: ReturnType<typeof vi.fn>;
  };
  let router: Router;

  const mockDoc: DocumentItem = {
    id: 42,
    title: 'Master Service Agreement',
    fileName: 'msa.pdf',
    contentType: 'application/pdf',
    fileSizeBytes: 5120,
    createdAt: '2026-01-01',
    categoryId: 1,
    categoryName: 'Legal',
  };

  const mockCats: CategoryItem[] = [{ id: 1, name: 'Legal' }];

  beforeEach(async () => {
    docServiceSpy = {
      getDocumentById: vi.fn().mockReturnValue(of(mockDoc)),
      getCategories: vi.fn().mockReturnValue(of(mockCats)),
      updateDocument: vi.fn(),
      deleteDocument: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [DocumentDetail],
      providers: [
        provideRouter([]),
        { provide: DocumentService, useValue: docServiceSpy },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => (key === 'id' ? '42' : null),
              },
            },
          },
        },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate');

    fixture = TestBed.createComponent(DocumentDetail);
    component = fixture.componentInstance;
  });

  it('should create and fetch document #42 on init', () => {
    fixture.detectChanges();

    expect(component.documentId()).toBe(42);
    expect(docServiceSpy.getDocumentById).toHaveBeenCalledWith(42);
    expect(component.document()?.title).toBe('Master Service Agreement');
    expect(component.isLoading()).toBe(false);
  });

  it('should toggle isEditing state', () => {
    fixture.detectChanges();
    expect(component.isEditing()).toBe(false);

    component.toggleEdit();
    expect(component.isEditing()).toBe(true);
  });

  it('should update document and display success message', () => {
    docServiceSpy.updateDocument.mockReturnValue(of(void 0));
    fixture.detectChanges();

    component.handleSave({ title: 'Updated MSA', categoryId: 1 });

    expect(docServiceSpy.updateDocument).toHaveBeenCalledWith(42, {
      title: 'Updated MSA',
      categoryId: 1,
    });
    expect(component.statusMessage()).toContain('Document updated successfully!');
    expect(component.isEditing()).toBe(false);
  });

  it('should delete document and navigate back to dashboard', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    docServiceSpy.deleteDocument.mockReturnValue(of(void 0));
    fixture.detectChanges();

    component.deleteDocument();

    expect(docServiceSpy.deleteDocument).toHaveBeenCalledWith(42);
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('should handle fetch errors gracefully', () => {
    docServiceSpy.getDocumentById.mockReturnValue(throwError(() => new Error('404 Not Found')));
    component.fetchData();
    fixture.detectChanges();

    expect(component.errorMessage()).toContain('Could not load document #42');
    expect(component.isLoading()).toBe(false);
  });
});
