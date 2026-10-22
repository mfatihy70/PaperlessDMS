import { Component, input, output, OnInit, effect } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { DocumentItem, CategoryItem, UpdateDocumentDto } from '../../services/document.service';

@Component({
  selector: 'app-document-edit',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './document-edit.html',
  styleUrls: ['./document-edit.scss'],
})
export class DocumentEdit implements OnInit {
  document = input.required<DocumentItem>();
  categories = input.required<CategoryItem[]>();
  isSaving = input<boolean>(false);

  save = output<UpdateDocumentDto>();
  cancel = output<void>();

  form!: FormGroup;

  constructor(private fb: FormBuilder) {
    effect(() => {
      const doc = this.document();
      if (this.form && doc) {
        this.form.patchValue({
          title: doc.title,
          categoryId: doc.categoryId ?? null,
        });
      }
    });
  }

  ngOnInit(): void {
    this.form = this.fb.group({
      title: [
        this.document().title,
        [Validators.required, Validators.minLength(3), Validators.maxLength(100)],
      ],
      categoryId: [this.document().categoryId ?? null],
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.save.emit(this.form.value);
  }
}
