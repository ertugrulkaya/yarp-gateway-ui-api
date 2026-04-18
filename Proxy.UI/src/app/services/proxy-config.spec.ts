import { TestBed } from '@angular/core/testing';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { ProxyConfigService } from './proxy-config';

describe('ProxyConfigService', () => {
  let service: ProxyConfigService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ProxyConfigService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
