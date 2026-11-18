import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DocumentView } from './document-view';
import { DocumentItem } from '../../services/document.service';

describe('DocumentView', () => {
  let fixture: ComponentFixture<DocumentView>;
  let component: DocumentView;

  const mockDoc: DocumentItem = {
    id: 1,
    title: 'Insurance Policy',
    fileName: 'insurance.pdf',
    contentType: 'application/pdf',
    fileSizeBytes: 2048,
    createdAt: new Date().toISOString(),
    categoryId: 3,
    categoryName: 'Legal',
    hasFile: true,
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DocumentView],
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentView);
    component = fixture.componentInstance;

    fixture.componentRef.setInput('document', mockDoc);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should display file name, content type, and category badge', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('insurance.pdf');
    expect(compiled.textContent).toContain('application/pdf');
    expect(compiled.textContent).toContain('Legal');
  });

  it('should display Unassigned badge when categoryName is missing', () => {
    const docNoCat: DocumentItem = { ...mockDoc, categoryId: null, categoryName: undefined };
    fixture.componentRef.setInput('document', docNoCat);
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Unassigned');
  });
});
