import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { of, Subject, throwError } from 'rxjs';
import { Search } from './search';
import { DataChangeEvent, DocumentItem, DocumentService } from '../../services/document.service';

describe('Search', () => {
  let component: Search;
  let fixture: ComponentFixture<Search>;
  let documentService: {
    getDocuments: ReturnType<typeof vi.fn>;
    dataChanged$: Subject<DataChangeEvent>;
  };

  const documents: DocumentItem[] = [
    {
      id: 1,
      title: 'Invoice',
      fileName: 'invoice-2026.pdf',
      contentType: 'application/pdf',
      fileSizeBytes: 1024,
      createdAt: '2026-01-01',
      categoryId: 1,
      categoryName: 'Finance',
      hasFile: true,
    },
    {
      id: 2,
      title: 'Agreement',
      fileName: 'agreement.docx',
      contentType: 'application/docx',
      fileSizeBytes: 2048,
      createdAt: '2026-01-02',
      categoryId: 2,
      categoryName: 'Legal',
      hasFile: true,
    },
  ];

  beforeEach(async () => {
    documentService = {
      getDocuments: vi.fn().mockReturnValue(of(documents)),
      dataChanged$: new Subject<DataChangeEvent>(),
    };

    await TestBed.configureTestingModule({
      imports: [Search],
      providers: [provideRouter([]), { provide: DocumentService, useValue: documentService }],
    }).compileComponents();

    fixture = TestBed.createComponent(Search);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  function searchFor(term: string): void {
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = term;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should filter by file name and category, ignoring case', () => {
    searchFor('FIN');

    expect(component.filteredDocuments().map((document) => document.id)).toEqual([1]);

    searchFor('agreement');
    expect(component.filteredDocuments().map((document) => document.id)).toEqual([2]);
  });

  it('should show document results that link to the preview page', () => {
    searchFor('legal');

    const result = fixture.nativeElement.querySelector('.search-results a') as HTMLAnchorElement;
    expect(result.textContent).toContain('agreement.docx');
    expect(result.getAttribute('href')).toBe('/documents/2');
  });

  it('should reload documents after document or category changes', () => {
    const updatedDocuments = [
      ...documents,
      {
        id: 3,
        title: 'New invoice',
        fileName: 'new-invoice.pdf',
        contentType: 'application/pdf',
        fileSizeBytes: 512,
        createdAt: '2026-01-03',
        categoryName: 'Finance',
        hasFile: true,
      },
    ];
    documentService.getDocuments.mockReturnValue(of(updatedDocuments));

    searchFor('new-invoice');
    expect(component.filteredDocuments()).toHaveLength(0);

    documentService.dataChanged$.next({ entity: 'document', action: 'created' });
    fixture.detectChanges();

    expect(component.filteredDocuments().map((document) => document.id)).toEqual([3]);

    documentService.dataChanged$.next({ entity: 'category', action: 'updated' });
    expect(documentService.getDocuments).toHaveBeenCalledTimes(3);
  });

  it('should show an error when documents cannot be loaded', () => {
    documentService.getDocuments.mockReturnValue(throwError(() => new Error('offline')));
    fixture = TestBed.createComponent(Search);
    component = fixture.componentInstance;
    fixture.detectChanges();
    searchFor('invoice');

    expect(component.errorMessage()).toBe('Could not load documents for search.');
    expect(fixture.nativeElement.textContent).toContain('Could not load documents for search.');
  });
});
