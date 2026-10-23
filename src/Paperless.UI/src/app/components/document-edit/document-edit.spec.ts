import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DocumentEdit } from './document-edit';
import { DocumentItem } from '../../services/document.service';

describe('DocumentEdit', () => {
  let fixture: ComponentFixture<DocumentEdit>;
  let component: DocumentEdit;

  const mockDoc: DocumentItem = {
    id: 1,
    title: 'Quarterly Report',
    fileName: 'report.pdf',
    contentType: 'application/pdf',
    fileSizeBytes: 1024,
    createdAt: '2026-01-01',
    categoryId: 2,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentEdit],
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentEdit);
    component = fixture.componentInstance;

    fixture.componentRef.setInput('document', mockDoc);
    fixture.componentRef.setInput('categories', [{ id: 2, name: 'Reports' }]);
    fixture.componentRef.setInput('isSaving', false);
    fixture.detectChanges();
  });

  it('should create and populate form with current document values', () => {
    expect(component).toBeTruthy();
    expect(component.form.get('title')?.value).toBe('Quarterly Report');
    expect(component.form.get('categoryId')?.value).toBe(2);
  });

  it('should emit save with updated DTO when valid', () => {
    const emitSpy = vi.spyOn(component.save, 'emit');

    component.form.patchValue({ title: 'Annual Report 2026', categoryId: null });
    component.onSubmit();

    expect(emitSpy).toHaveBeenCalledWith({
      title: 'Annual Report 2026',
      categoryId: null,
    });
  });

  it('should emit cancel event when cancel is called', () => {
    const cancelSpy = vi.spyOn(component.cancel, 'emit');
    component.cancel.emit();
    expect(cancelSpy).toHaveBeenCalled();
  });
});
