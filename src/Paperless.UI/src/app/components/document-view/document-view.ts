import { Component, input } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { DocumentItem } from '../../services/document.service';

@Component({
  selector: 'app-document-view',
  standalone: true,
  imports: [DatePipe, DecimalPipe],
  templateUrl: './document-view.html',
  styleUrls: ['./document-view.scss'],
})
export class DocumentView {
  document = input.required<DocumentItem>();
}
