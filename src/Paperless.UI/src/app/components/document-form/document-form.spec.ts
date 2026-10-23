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

  it('should create and initialize an invalid form', () => {
    expect(component).toBeTruthy();
    expect(component.form.valid).toBeFalsy();
  });

  it('should reject invalid file extensions and accept allowed formats', () => {
    const fileCtrl = component.form.get('fileName');

    fileCtrl?.setValue('malicious.exe');
    expect(fileCtrl?.valid).toBeFalsy();

    fileCtrl?.setValue('archive.zip');
    expect(fileCtrl?.valid).toBeFalsy();

    fileCtrl?.setValue('scan.pdf');
    expect(fileCtrl?.valid).toBe(true);
  });

  it('should require title between 3 and 100 characters', () => {
    const titleCtrl = component.form.get('title');

    titleCtrl?.setValue('');
    expect(titleCtrl?.hasError('required')).toBe(true);

    titleCtrl?.setValue('ab');
    expect(titleCtrl?.hasError('minlength')).toBe(true);

    titleCtrl?.setValue('Valid Document Title');
    expect(titleCtrl?.valid).toBe(true);
  });

  it('should emit saveDocument when submitting a valid form', () => {
    const emitSpy = vi.spyOn(component.saveDocument, 'emit');

    component.form.setValue({
      title: 'Valid Contract',
      fileName: 'contract.pdf',
      fileSizeBytes: 2048,
      categoryId: 1,
    });
    fixture.detectChanges();

    component.onSubmit();

    expect(emitSpy).toHaveBeenCalledWith({
      title: 'Valid Contract',
      fileName: 'contract.pdf',
      fileSizeBytes: 2048,
      categoryId: 1,
    });
  });

  it('should reset form values when reset() is called', () => {
    component.form.patchValue({ title: 'Temporary Title' });
    component.reset();

    expect(component.form.get('title')?.value).toBeNull();
    expect(component.form.get('fileSizeBytes')?.value).toBe(1024);
  });
});
