import { Component, input, output, computed } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DocumentItem, CategoryItem } from '../../services/document.service';

@Component({
  selector: 'app-document-table',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterModule],
  templateUrl: './document-table.html',
  styleUrls: ['./document-table.scss'],
})
export class DocumentTable {
  documents = input.required<DocumentItem[]>();
  categories = input.required<CategoryItem[]>();
  selectedCategoryId = input<number | null>(null);
  isLoading = input<boolean>(false);

  searchChange = output<string>();
  categoryFilterChange = output<number | null>();
  delete = output<number>();

  activeCategory = computed(() => {
    const id = this.selectedCategoryId();
    if (id === null) return null;
    return this.categories().find((c) => c.id === id) ?? null;
  });

  onSearch(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.searchChange.emit(val);
  }

  onFilter(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.categoryFilterChange.emit(val ? Number(val) : null);
  }

  onDelete(event: Event, id: number): void {
    event.stopPropagation();
    if (confirm('Are you sure you want to delete this document?')) {
      this.delete.emit(id);
    }
  }
}
