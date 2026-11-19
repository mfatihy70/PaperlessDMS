import { ComponentFixture, TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CategoryForm } from './category-form';
import { CategoryItem } from '../../services/document.service';

describe('CategoryForm', () => {
  let fixture: ComponentFixture<CategoryForm>;
  let component: CategoryForm;

  const mockCategories: CategoryItem[] = [
    { id: 1, name: 'Legal', description: 'Contracts and policies' },
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CategoryForm],
    }).compileComponents();

    fixture = TestBed.createComponent(CategoryForm);
    component = fixture.componentInstance;

    fixture.componentRef.setInput('categories', mockCategories);
    fixture.componentRef.setInput('isSaving', false);
    fixture.detectChanges();
  });

  it('should create and validate name requirements', () => {
    expect(component).toBeTruthy();
    const nameCtrl = component.form.get('name');

    nameCtrl?.setValue('');
    expect(nameCtrl?.hasError('required')).toBe(true);

    nameCtrl?.setValue('Finance');
    expect(nameCtrl?.valid).toBe(true);
  });

  it('should emit saveCategory on valid form submission', () => {
    const emitSpy = vi.spyOn(component.saveCategory, 'emit');

    component.form.setValue({ name: 'Invoices', description: 'Vendor bills' });
    component.onSubmit();

    expect(emitSpy).toHaveBeenCalledWith({
      name: 'Invoices',
      description: 'Vendor bills',
    });
  });

  it('should toggle inline editing mode and populate editForm', () => {
    component.startEdit(mockCategories[0]);
    expect(component.editingId).toBe(1);
    expect(component.editForm.get('name')?.value).toBe('Legal');
    expect(component.editForm.get('description')?.value).toBe('Contracts and policies');

    component.cancelEdit();
    expect(component.editingId).toBeNull();
  });

  it('should emit editCategory with updated payload', () => {
    const emitSpy = vi.spyOn(component.editCategory, 'emit');

    component.startEdit(mockCategories[0]);
    component.editForm.setValue({ name: 'Legal & Compliance', description: 'Updated' });
    component.submitEdit(1);

    expect(emitSpy).toHaveBeenCalledWith({
      id: 1,
      data: { name: 'Legal & Compliance', description: 'Updated' },
    });
    expect(component.editingId).toBe(1);
  });

  it('should emit deleteCategory when user confirms prompt', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const emitSpy = vi.spyOn(component.deleteCategory, 'emit');

    component.onDelete(1);
    expect(emitSpy).toHaveBeenCalledWith(1);
  });
});
