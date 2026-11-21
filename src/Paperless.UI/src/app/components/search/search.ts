import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { EMPTY, catchError, merge, of, switchMap } from 'rxjs';
import { DocumentItem, DocumentService } from '../../services/document.service';

@Component({
  standalone: true,
  imports: [RouterModule],
  selector: 'app-search',
  styleUrl: './search.scss',
  templateUrl: './search.html',
})
export class Search implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly documents = signal<DocumentItem[]>([]);
  readonly searchTerm = signal('');
  readonly errorMessage = signal('');
  readonly filteredDocuments = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    if (!term) return [];

    return this.documents().filter(
      (document) =>
        document.fileName.toLowerCase().includes(term) ||
        (document.categoryName ?? '').toLowerCase().includes(term),
    );
  });

  constructor(private readonly api: DocumentService) {}

  ngOnInit(): void {
    merge(of(undefined), this.api.dataChanged$)
      .pipe(
        switchMap(() =>
          this.api.getDocuments().pipe(
            catchError(() => {
              this.errorMessage.set('Could not load documents for search.');
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((documents) => {
        this.documents.set(documents);
        this.errorMessage.set('');
      });
  }

  onSearch(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  clearSearch(): void {
    this.searchTerm.set('');
  }
}
