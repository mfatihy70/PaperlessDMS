import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DocumentTable } from './document-table';
import { DocumentItem, CategoryItem } from '../../services/document.service';

describe('DocumentTable', () => {
  let fixture: ComponentFixture<DocumentTable>;
  let component: DocumentTable;

  const mockCategories: CategoryItem[] = [
    { id: 1, name: 'Invoices', description: 'Supplier invoices' },
    { id: 2, name: 'HR', description: 'Employee records' },
  ];

  const mockDocs: DocumentItem[] = [
    {
      id: 10,
      title: 'AWS October Bill',
      fileName: 'aws_oct.pdf',
      contentType: 'application/pdf',
      fileSizeBytes: 2048,
      createdAt: '2026-01-01',
      categoryId: 1,
      categoryName: 'Invoices',
      hasFile: true,
    },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentTable],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentTable);
    component = fixture.componentInstance;

    fixture.componentRef.setInput('documents', mockDocs);
    fixture.componentRef.setInput('categories', mockCategories);
    fixture.componentRef.setInput('selectedCategoryId', null);
    fixture.componentRef.setInput('isLoading', false);
    fixture.detectChanges();
  });

  it('should create and render documents list', () => {
    expect(component).toBeTruthy();
    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('AWS October Bill');
  });

  it('should compute activeCategory and display description banner when category is selected', () => {
    fixture.componentRef.setInput('selectedCategoryId', 1);
    fixture.detectChanges();

    expect(component.activeCategory()?.name).toBe('Invoices');

    const banner = fixture.nativeElement.querySelector('.alert-light');
    expect(banner).toBeTruthy();
    expect(banner.textContent).toContain('Supplier invoices');
  });

  it('should emit searchChange on search input typing', () => {
    const emitSpy = vi.spyOn(component.searchChange, 'emit');

    const input = fixture.nativeElement.querySelector('input[type="text"]');
    input.value = 'AWS';
    input.dispatchEvent(new Event('input'));

    expect(emitSpy).toHaveBeenCalledWith('AWS');
  });

  it('should emit categoryFilterChange on dropdown selection', () => {
    const emitSpy = vi.spyOn(component.categoryFilterChange, 'emit');

    const select = fixture.nativeElement.querySelector('select');
    select.value = '2';
    select.dispatchEvent(new Event('change'));

    expect(emitSpy).toHaveBeenCalledWith(2);
  });

  it('should emit delete event when confirmed', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const emitSpy = vi.spyOn(component.delete, 'emit');

    const deleteBtn = fixture.nativeElement.querySelector('.btn-outline-danger');
    deleteBtn.click();

    expect(emitSpy).toHaveBeenCalledWith(10);
  });
});
