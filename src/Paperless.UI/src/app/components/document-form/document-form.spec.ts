import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DocumentForm } from './document-form';

describe('DocumentForm', () => {
  let fixture: ComponentFixture<DocumentForm>;
  let component: DocumentForm;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentForm],
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentForm);
    component = fixture.componentInstance;

    fixture.componentRef.setInput('categories', [{ id: 1, name: 'Invoices' }]);
    fixture.componentRef.setInput('isSaving', false);
    fixture.detectChanges();
  });

  it('should accept a selected ODT file and emit the original File', () => {
    const file = new File(['archive'], 'report.odt', {
      type: 'application/vnd.oasis.opendocument.text',
    });
    component.onFileSelected({ target: { files: [file], value: '' } } as unknown as Event);

    const emitSpy = vi.spyOn(component.saveDocument, 'emit');
    component.form.patchValue({ categoryId: 1 });
    component.onSubmit();

    expect(emitSpy).toHaveBeenCalledWith({ file, categoryId: 1 });
  });

  it('should reject empty files and require a file before submitting', () => {
    const input = { files: [new File([], 'empty.pdf')], value: 'empty.pdf' };
    component.onFileSelected({ target: input } as unknown as Event);
    expect(component.selectedFile()).toBeNull();
    expect(component.fileError()).toContain('empty');

    component.onSubmit();
    expect(component.fileError()).toContain('Choose a file');
  });

  it('should reset the selected file and category', () => {
    const file = new File(['text'], 'notes.md', { type: 'text/markdown' });
    component.onFileSelected({ target: { files: [file], value: '' } } as unknown as Event);
    component.form.patchValue({ categoryId: 1 });

    component.reset();

    expect(component.selectedFile()).toBeNull();
    expect(component.form.get('categoryId')?.value).toBeNull();
  });
});
