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
    hasFile: true,
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

  it('should create and populate the current category', () => {
    expect(component).toBeTruthy();
    expect(component.form.get('categoryId')?.value).toBe(2);
  });

  it('should emit a replacement file and category', () => {
    const emitSpy = vi.spyOn(component.save, 'emit');
    const file = new File(['updated'], 'updated.docx', {
      type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    });
    component.onFileSelected({ target: { files: [file], value: '' } } as unknown as Event);

    component.form.patchValue({ categoryId: null });
    component.onSubmit();

    expect(emitSpy).toHaveBeenCalledWith({ file, categoryId: null });
  });

  it('should emit cancel event when cancel is called', () => {
    const cancelSpy = vi.spyOn(component.cancel, 'emit');
    component.cancel.emit();
    expect(cancelSpy).toHaveBeenCalled();
  });
});
