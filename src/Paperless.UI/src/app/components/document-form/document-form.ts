import { Component, input, output, OnInit } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CategoryItem, CreateDocumentDto } from '../../services/document.service';

@Component({
  selector: 'app-document-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './document-form.html',
  styleUrls: ['./document-form.scss'],
})
export class DocumentForm implements OnInit {
  categories = input.required<CategoryItem[]>();
  isSaving = input<boolean>(false);
  saveDocument = output<CreateDocumentDto>();

  form!: FormGroup;

  constructor(private fb: FormBuilder) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
      fileName: [
        '',
        [Validators.required, Validators.pattern(/^[a-zA-Z0-9_\-\.]+\.(pdf|docx|txt|png|jpg)$/i)],
      ],
      fileSizeBytes: [1024, [Validators.required, Validators.min(1)]],
      categoryId: [null],
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saveDocument.emit(this.form.value);
  }

  reset(): void {
    this.form.reset({ fileSizeBytes: 1024, categoryId: null });
  }
}
