import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { forkJoin } from 'rxjs';
import { DocumentService, DocumentItem, CategoryItem } from '../../services/document.service';
import { DocumentView } from '../../components/document-view/document-view';
import { DocumentEdit, DocumentEditValue } from '../../components/document-edit/document-edit';

@Component({
  selector: 'app-document-detail',
  standalone: true,
  imports: [RouterModule, DocumentView, DocumentEdit],
  templateUrl: './document-detail.html',
  styleUrls: ['./document-detail.scss'],
})
export class DocumentDetail implements OnInit {
  documentId = signal<number>(0);
  document = signal<DocumentItem | null>(null);
  categories = signal<CategoryItem[]>([]);

  isLoading = signal<boolean>(true);
  isSaving = signal<boolean>(false);
  isEditing = signal<boolean>(false);
  statusMessage = signal<string>('');
  errorMessage = signal<string>('');

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private api: DocumentService,
  ) {}

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    const parsedId = Number(idParam);

    if (!idParam || isNaN(parsedId)) {
      this.router.navigate(['/']);
      return;
    }

    this.documentId.set(parsedId);
    this.fetchData();
  }

  fetchData(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');

    forkJoin({
      categories: this.api.getCategories(),
      document: this.api.getDocumentById(this.documentId()),
    }).subscribe({
      next: (res) => {
        this.categories.set(res.categories);
        this.document.set(res.document);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(
          `Could not load document #${this.documentId()}: ${err.message || 'Resource not found'}`,
        );
        this.isLoading.set(false);
      },
    });
  }

  toggleEdit(): void {
    this.isEditing.update((val) => !val);
  }

  handleSave(value: DocumentEditValue): void {
    this.isSaving.set(true);
    this.api.updateDocument(this.documentId(), value.file, value.categoryId).subscribe({
      next: () => {
        this.statusMessage.set('Document updated successfully!');
        this.isEditing.set(false);
        this.isSaving.set(false);
        this.fetchData();
        setTimeout(() => this.statusMessage.set(''), 3500);
      },
      error: (err) => {
        this.errorMessage.set('Update failed: ' + err.message);
        this.isSaving.set(false);
      },
    });
  }

  deleteDocument(): void {
    if (!confirm('Are you sure you want to permanently delete this document?')) return;

    this.api.deleteDocument(this.documentId()).subscribe({
      next: () => this.router.navigate(['/']),
      error: (err) => this.errorMessage.set('Delete failed: ' + err.message),
    });
  }
}
