import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  TuiButton,
  TuiLoader,
  TuiNotification,
  TuiScrollbar,
  TuiSurface,
  TuiTextfield,
} from '@taiga-ui/core';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { CollectionsService } from '../../../../client/services/collections.service';
import { CollectionDto } from '../../../../client/models/collection-dto';
import { CreateCollectionRequest } from '../../../../client/models/create-collection-request';
import { UpdateCollectionRequest } from '../../../../client/models/update-collection-request';
import { ToastService } from '../../../../services/toast.service';

type CollectionFormControls = {
  name: FormControl<string>;
};

@Component({
  standalone: true,
  selector: 'app-collections-settings',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslocoModule,
    TuiButton,
    TuiTextfield,
    TuiSurface,
    TuiLoader,
    TuiNotification,
    TuiScrollbar,
  ],
  templateUrl: './collections.component.html',
  styleUrl: './collections.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CollectionsComponent implements OnInit {
  private readonly collectionsService = inject(CollectionsService);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  protected readonly collections = signal<CollectionDto[]>([]);
  protected readonly loadingCollections = signal(false);
  protected readonly savingCollection = signal(false);
  protected readonly deletingCollectionId = signal<string | null>(null);
  protected readonly editingCollectionId = signal<string | null>(null);

  protected readonly sortedCollections = computed(() =>
    [...this.collections()].sort((a, b) => this.compareNames(a.name, b.name))
  );

  protected readonly collectionForm = new FormGroup<CollectionFormControls>({
    name: new FormControl<string>('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(100)] }),
  });

  async ngOnInit(): Promise<void> {
    await this.loadCollections();
  }

  protected async reload(): Promise<void> {
    await this.loadCollections();
  }

  protected startCreate(): void {
    this.editingCollectionId.set(null);
    this.collectionForm.reset({ name: '' });
    this.collectionForm.markAsPristine();
  }

  protected editCollection(collection: CollectionDto): void {
    const id = collection.id ?? null;
    if (!id) {
      return;
    }
    this.editingCollectionId.set(id);
    this.collectionForm.setValue({
      name: (collection.name ?? '').trim(),
    });
    this.collectionForm.markAsPristine();
  }

  protected async saveCollection(): Promise<void> {
    this.collectionForm.markAllAsTouched();
    if (this.collectionForm.invalid) {
      this.toast.error(this.transloco.translate('Settings.Collections.Form.ValidationError') || 'Validierungsfehler');
      return;
    }

    const raw = this.collectionForm.getRawValue();
    const trimmedName = raw.name.trim();
    if (!trimmedName) {
      this.collectionForm.controls.name.setValue('');
      this.toast.error(this.transloco.translate('Settings.Collections.Form.ValidationError') || 'Validierungsfehler');
      return;
    }

    const editingId = this.editingCollectionId();

    this.savingCollection.set(true);
    try {
      if (editingId) {
        const request: UpdateCollectionRequest = {
          name: trimmedName,
        };
        const response = await firstValueFrom(
          this.collectionsService.apiCollectionsIdPut$Json({ id: editingId, body: request })
        );
        if (response.success === false && response.message) {
          this.toast.error(response.message);
          return;
        }
        const updated = response.data ?? { id: editingId, name: trimmedName };
        this.updateCollectionList(updated);
        const successMsg = ((response.message ?? this.transloco.translate('Settings.Collections.Toast.Saved')) || 'Gespeichert') as string;
        this.toast.success(successMsg);
        this.collectionForm.markAsPristine();
      } else {
        const request: CreateCollectionRequest = {
          name: trimmedName,
        };
        const response = await firstValueFrom(this.collectionsService.apiCollectionsPost$Json({ body: request }));
        if (response.success === false && response.message) {
          this.toast.error(response.message);
          return;
        }
        const created = response.data ?? { id: undefined, name: trimmedName };
        this.collections.update(current => [...current, created]);
        const successMsg = ((response.message ?? this.transloco.translate('Settings.Collections.Toast.Saved')) || 'Gespeichert') as string;
        this.toast.success(successMsg);
        if (created.id) {
          this.editCollection(created);
        } else {
          this.collectionForm.markAsPristine();
        }
      }
    } catch (error) {
      this.toast.error(this.toErrorMessage(error, this.transloco.translate('Settings.Collections.Toast.SaveError') || 'Fehler beim Speichern'));
    } finally {
      this.savingCollection.set(false);
    }
  }

  protected async deleteCollection(collection: CollectionDto): Promise<void> {
    const id = collection.id ?? null;
    if (!id) {
      return;
    }
    const confirmMessage = this.transloco.translate('Settings.Collections.Delete.Confirm', {
      name: collection.name ?? (this.transloco.translate('Settings.Collections.Untitled') || 'Unbenannt'),
    }) || `Möchten Sie die Sammlung "${collection.name}" wirklich löschen?`;
    
    if (!window.confirm(confirmMessage)) {
      return;
    }

    this.deletingCollectionId.set(id);
    try {
      await firstValueFrom(this.collectionsService.apiCollectionsIdDelete({ id }));
      this.collections.update(current => current.filter(c => c.id !== id));
      if (this.editingCollectionId() === id) {
        this.startCreate();
      }
      this.toast.success(this.transloco.translate('Settings.Collections.Toast.Deleted') || 'Gelöscht');
    } catch (error) {
      this.toast.error(this.toErrorMessage(error, this.transloco.translate('Settings.Collections.Toast.DeleteError') || 'Fehler beim Löschen'));
    } finally {
      this.deletingCollectionId.set(null);
    }
  }

  protected trackByCollectionId(_: number, collection: CollectionDto): string {
    return collection.id ?? collection.name ?? '';
  }

  protected isDeleting(collection: CollectionDto): boolean {
    const id = collection.id ?? null;
    if (!id) {
      return false;
    }
    return this.deletingCollectionId() === id;
  }

  private async loadCollections(): Promise<void> {
    this.loadingCollections.set(true);
    try {
      const response = await firstValueFrom(this.collectionsService.apiCollectionsGet$Json());
      if (response.success === false && response.message) {
        this.toast.error(response.message);
        return;
      }
      const collections = response.data ?? [];
      this.collections.set(collections);
    } catch (error) {
      this.toast.error(this.toErrorMessage(error, this.transloco.translate('Settings.Collections.Toast.LoadError') || 'Fehler beim Laden'));
    } finally {
      this.loadingCollections.set(false);
    }
  }

  private updateCollectionList(updated: CollectionDto): void {
    this.collections.update(current => {
      let replaced = false;
      const list = current.map(collection => {
        if (collection.id === updated.id) {
          replaced = true;
          return { ...collection, ...updated };
        }
        return collection;
      });
      if (!replaced) {
        list.push(updated);
      }
      return list;
    });
  }

  private compareNames(a?: string | null, b?: string | null): number {
    const left = (a ?? '').trim().toLocaleLowerCase();
    const right = (b ?? '').trim().toLocaleLowerCase();
    return left.localeCompare(right, undefined, { sensitivity: 'base' });
  }

  private toErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const message = error.error?.message ?? error.message;
      if (typeof message === 'string' && message.length > 0) {
        return message;
      }
    }
    if (typeof error === 'string' && error.length > 0) {
      return error;
    }
    return fallback;
  }
}
