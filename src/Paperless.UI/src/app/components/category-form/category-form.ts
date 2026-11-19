import { Component, input, output, OnInit } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import {
  CategoryItem,
  CreateCategoryDto,
  UpdateCategoryDto,
} from '../../services/document.service';

@Component({
  selector: 'app-category-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './category-form.html',
  styleUrls: ['./category-form.scss'],
})
export class CategoryForm implements OnInit {
  categories = input.required<CategoryItem[]>();
  isSaving = input<boolean>(false);

  saveCategory = output<CreateCategoryDto>();
  editCategory = output<{ id: number; data: UpdateCategoryDto }>();
  deleteCategory = output<number>();

  form!: FormGroup;
  editingId: number | null = null;
  editForm!: FormGroup;

  constructor(private fb: FormBuilder) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(/\S/)]],
      description: ['', [Validators.maxLength(200)]],
    });

    this.editForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100), Validators.pattern(/\S/)]],
      description: ['', [Validators.maxLength(200)]],
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saveCategory.emit(this.form.value);
  }

  startEdit(cat: CategoryItem): void {
    this.editingId = cat.id;
    this.editForm.patchValue({
      name: cat.name,
      description: cat.description || '',
    });
  }

  cancelEdit(): void {
    this.editingId = null;
  }

  submitEdit(id: number): void {
    if (this.isSaving()) return;

    if (this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }

    this.editCategory.emit({ id, data: this.editForm.value });
  }

  onDelete(id: number): void {
    if (confirm('Delete this category? Documents assigned to it will become unassigned.')) {
      this.deleteCategory.emit(id);
    }
  }

  reset(): void {
    this.form.reset();
  }
}
