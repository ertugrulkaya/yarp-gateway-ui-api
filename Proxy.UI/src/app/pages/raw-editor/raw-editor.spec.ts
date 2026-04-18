import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RawEditor } from './raw-editor';

describe('RawEditor', () => {
  let component: RawEditor;
  let fixture: ComponentFixture<RawEditor>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RawEditor],
    }).compileComponents();

    fixture = TestBed.createComponent(RawEditor);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
