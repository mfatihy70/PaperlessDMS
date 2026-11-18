import { Component, input, output, OnInit, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup } from '@angular/forms';
import { CategoryItem, MAX_DOCUMENT_SIZE_BYTES } from '../../services/document.service';

/** The selected original file and optional category to upload. */
export interface DocumentFormValue {
  file: File;
  categoryId: number | null;
}

@Component({
  selector: 'app-document-form',
  standalone: true,
  imports: [ReactiveFormsModule, DecimalPipe],
  templateUrl: './document-form.html',
  styleUrls: ['./document-form.scss'],
})
export class DocumentForm implements OnInit {
  categories = input.required<CategoryItem[]>();
  isSaving = input<boolean>(false);
  saveDocument = output<DocumentFormValue>();

  form!: FormGroup;

  selectedFile = signal<File | null>(null);
  fileError = signal<string | null>(null);

  constructor(private fb: FormBuilder) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      categoryId: [null],
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
    const file = this.selectedFile();
    if (!file) {
      this.fileError.set('Choose a file to upload.');
      return;
    }

    this.saveDocument.emit({
      file,
      categoryId: this.form.get('categoryId')?.value ?? null,
    });
  }

  reset(): void {
    this.selectedFile.set(null);
    this.fileError.set(null);
    this.form.reset({ categoryId: null });
  }
}
