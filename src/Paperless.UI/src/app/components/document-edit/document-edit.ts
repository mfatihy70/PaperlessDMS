import { Component, input, output, OnInit, effect, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
import {
  DocumentItem,
  CategoryItem,
  MAX_DOCUMENT_SIZE_BYTES,
} from '../../services/document.service';

export interface DocumentEditValue {
  file: File | null;
  categoryId: number | null;
}

@Component({
  selector: 'app-document-edit',
  standalone: true,
  imports: [ReactiveFormsModule, DecimalPipe],
  templateUrl: './document-edit.html',
  styleUrls: ['./document-edit.scss'],
})
export class DocumentEdit implements OnInit {
  document = input.required<DocumentItem>();
  categories = input.required<CategoryItem[]>();
  isSaving = input<boolean>(false);

  save = output<DocumentEditValue>();
  cancel = output<void>();

  form!: FormGroup;
  selectedFile = signal<File | null>(null);
  fileError = signal<string | null>(null);

  constructor(private fb: FormBuilder) {
    effect(() => {
      const doc = this.document();
      if (this.form && doc) {
        this.form.patchValue({ categoryId: doc.categoryId ?? null });
      }
    });
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      categoryId: [this.document().categoryId ?? null],
    });
  }

  onFileSelected(event: Event): void {
    const inputEl = event.target as HTMLInputElement;
    const file = inputEl.files?.[0] ?? null;
    this.fileError.set(null);
    if (!file) return;

    if (file.size === 0) {
      this.fileError.set('The selected file is empty.');
      inputEl.value = '';
      return;
    }
    if (file.size > MAX_DOCUMENT_SIZE_BYTES) {
      this.fileError.set('The file is larger than 25 MiB.');
      inputEl.value = '';
      return;
    }
    this.selectedFile.set(file);
  }

  removeFile(): void {
    this.selectedFile.set(null);
    this.fileError.set(null);
  }

  onSubmit(): void {
    this.save.emit({
      file: this.selectedFile(),
      categoryId: this.form.get('categoryId')?.value ?? null,
    });
  }
}
