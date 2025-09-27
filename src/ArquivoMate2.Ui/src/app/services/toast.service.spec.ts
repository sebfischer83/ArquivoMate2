import { of } from 'rxjs';
import { TuiAlertService } from '@taiga-ui/core';
import { ToastService } from './toast.service';

// Simple mock for TuiAlertService
class MockAlertService {
  opened: string[] = [];
  open(message: string, _opts: any) {
    this.opened.push(message);
    return of(true);
  }
}

describe('ToastService', () => {
  let service: ToastService;
  let mock: MockAlertService;

  beforeEach(() => {
    mock = new MockAlertService();
    service = new ToastService(mock as unknown as TuiAlertService);
  });

  it('should show first error message', () => {
    service.error('Network error');
    expect(mock.opened).toEqual(['Network error']);
  });

  it('should suppress duplicate error within window', (done) => {
    service.error('Network error');
    service.error('Network error');
    expect(mock.opened).toEqual(['Network error']);
    setTimeout(() => {
      // still inside 2s window (we wait 100ms) so suppressed
      service.error('Network error');
      expect(mock.opened).toEqual(['Network error']);
      done();
    }, 100);
  });

  it('should allow different messages', () => {
    service.error('A');
    service.error('B');
    expect(mock.opened).toEqual(['A', 'B']);
  });

  it('should allow forcing duplicate', () => {
    service.error('Same');
    service.error('Same', { force: true });
    expect(mock.opened).toEqual(['Same', 'Same']);
  });
});
