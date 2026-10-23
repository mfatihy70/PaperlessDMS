import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { of, throwError } from 'rxjs';
import { Dashboard } from './dashboard';
import { DocumentService, DocumentItem, CategoryItem } from '../../services/document.service';

describe('Dashboard Page', () => {
  let fixture: ComponentFixture<Dashboard>;
  let component: Dashboard;
  let docServiceSpy: ReturnType<typeof vi.fn> & {
    getDocuments: ReturnType<typeof vi.fn>;
    getCategories: ReturnType<typeof vi.fn>;
    createDocument: ReturnType<typeof vi.fn>;
    createCategory: ReturnType<typeof vi.fn>;
    updateCategory: ReturnType<typeof vi.fn>;
    deleteCategory: ReturnType<typeof vi.fn>;
    deleteDocument: ReturnType<typeof vi.fn>;
  };

  const mockDocs: DocumentItem[] = [
    {
      id: 1,
      title: 'Server Bill',
      fileName: 'bill.pdf',
      contentType: 'pdf',
      fileSizeBytes: 1024,
      createdAt: '2026-01-01',
      categoryId: 1,
    },
    {
      id: 2,
      title: 'Contract Alpha',
      fileName: 'contract.pdf',
      contentType: 'pdf',
      fileSizeBytes: 2048,
      createdAt: '2026-01-01',
      categoryId: 2,
    },
  ];

  const mockCats: CategoryItem[] = [
    { id: 1, name: 'Billing', description: 'Bills' },
    { id: 2, name: 'Legal', description: 'Contracts' },
  ];

  beforeEach(async () => {
    docServiceSpy = {
      getDocuments: vi.fn().mockReturnValue(of(mockDocs)),
      getCategories: vi.fn().mockReturnValue(of(mockCats)),
      createDocument: vi.fn(),
      createCategory: vi.fn(),
      updateCategory: vi.fn(),
      deleteCategory: vi.fn(),
      deleteDocument: vi.fn(),
    } as unknown as ReturnType<typeof vi.fn> & {
      getDocuments: ReturnType<typeof vi.fn>;
      getCategories: ReturnType<typeof vi.fn>;
      createDocument: ReturnType<typeof vi.fn>;
      createCategory: ReturnType<typeof vi.fn>;
      updateCategory: ReturnType<typeof vi.fn>;
      deleteCategory: ReturnType<typeof vi.fn>;
      deleteDocument: ReturnType<typeof vi.fn>;
    };

    await TestBed.configureTestingModule({
      imports: [Dashboard],
      providers: [provideRouter([]), { provide: DocumentService, useValue: docServiceSpy }],
    }).compileComponents();

    fixture = TestBed.createComponent(Dashboard);
    component = fixture.componentInstance;
  });

  it('should create and load data on init', () => {
    fixture.detectChanges(); // Triggers ngOnInit

    expect(docServiceSpy.getDocuments).toHaveBeenCalled();
    expect(docServiceSpy.getCategories).toHaveBeenCalled();
    expect(component.documents().length).toBe(2);
    expect(component.categories().length).toBe(2);
    expect(component.isLoading()).toBe(false);
  });

  it('should filter documents by search keyword', () => {
    fixture.detectChanges();

    component.onSearch('Server');
    expect(component.filteredDocuments().length).toBe(1);
    expect(component.filteredDocuments()[0].title).toBe('Server Bill');
  });

  it('should filter documents by category ID', () => {
    fixture.detectChanges();

    component.onFilterCategory(2);
    expect(component.filteredDocuments().length).toBe(1);
    expect(component.filteredDocuments()[0].categoryId).toBe(2);
  });

  it('should handle document upload delegation', () => {
    docServiceSpy.createDocument.mockReturnValue(of(mockDocs[0]));
    fixture.detectChanges();

    component.handleUpload({
      title: 'New',
      fileName: 'new.pdf',
      fileSizeBytes: 500,
      categoryId: null,
    });

    expect(docServiceSpy.createDocument).toHaveBeenCalled();
    expect(component.successMessage()).toContain('Document uploaded successfully!');
  });

  it('should handle category delete and clear filter if active', () => {
    docServiceSpy.deleteCategory.mockReturnValue(of(void 0));
    fixture.detectChanges();

    component.selectedCatId.set(2);
    component.handleDeleteCategory(2);

    expect(docServiceSpy.deleteCategory).toHaveBeenCalledWith(2);
    expect(component.selectedCatId()).toBeNull();
  });

  it('should show error banner when service load fails', () => {
    docServiceSpy.getDocuments.mockReturnValue(throwError(() => new Error('Connection failed')));
    component.loadData();
    fixture.detectChanges();

    expect(component.errorMessage()).toContain('Backend service starting up or unreachable');
    expect(component.isLoading()).toBe(false);
  });
});
