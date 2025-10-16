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
import { TuiCheckboxLabeled, TuiTag } from '@taiga-ui/kit';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { ShareGroupsService } from '../../../../client/services/share-groups.service';
import { ShareGroupDto } from '../../../../client/models/share-group-dto';
import { CreateShareGroupRequest } from '../../../../client/models/create-share-group-request';
import { UpdateShareGroupRequest } from '../../../../client/models/update-share-group-request';
import { UsersService } from '../../../../client/services/users.service';
import { UserDto } from '../../../../client/models/user-dto';
import { ToastService } from '../../../../services/toast.service';

type ShareGroupFormControls = {
  name: FormControl<string>;
  memberUserIds: FormControl<string[]>;
};

@Component({
  standalone: true,
  selector: 'app-share-groups-settings',
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
    TuiCheckboxLabeled,
    TuiTag,
  ],
  templateUrl: './share-groups.component.html',
  styleUrl: './share-groups.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShareGroupsComponent implements OnInit {
  private readonly shareGroupsService = inject(ShareGroupsService);
  private readonly usersService = inject(UsersService);
  private readonly toast = inject(ToastService);
  private readonly transloco = inject(TranslocoService);

  protected readonly groups = signal<ShareGroupDto[]>([]);
  protected readonly loadingGroups = signal(false);
  protected readonly loadingUsers = signal(false);
  protected readonly savingGroup = signal(false);
  protected readonly deletingGroupId = signal<string | null>(null);
  protected readonly editingGroupId = signal<string | null>(null);

  protected readonly users = signal<UserDto[]>([]);
  private readonly userNameMap = signal(new Map<string, string>());

  protected readonly sortedGroups = computed(() =>
    [...this.groups()].sort((a, b) => this.compareNames(a.name, b.name))
  );

  protected readonly groupForm = new FormGroup<ShareGroupFormControls>({
    name: new FormControl<string>('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(120)] }),
    memberUserIds: new FormControl<string[]>([], { nonNullable: true }),
  });

  async ngOnInit(): Promise<void> {
    await Promise.all([this.loadUsers(), this.loadGroups()]);
  }

  protected async reload(): Promise<void> {
    await this.loadGroups();
  }

  protected startCreate(): void {
    this.editingGroupId.set(null);
    this.groupForm.reset({ name: '', memberUserIds: [] });
    this.groupForm.markAsPristine();
  }

  protected editGroup(group: ShareGroupDto): void {
    const id = group.id ?? null;
    if (!id) {
      return;
    }
    this.editingGroupId.set(id);
    this.groupForm.setValue({
      name: (group.name ?? '').trim(),
      memberUserIds: this.toIds(group.memberUserIds),
    });
    this.groupForm.markAsPristine();
  }

  protected memberSummary(group: ShareGroupDto): string {
    const ids = this.toIds(group.memberUserIds);
    if (ids.length === 0) {
      return this.transloco.translate('Settings.ShareGroups.EmptyMembers');
    }
    const lookup = this.userNameMap();
    const fallback = this.transloco.translate('Settings.ShareGroups.MemberFallback');
    return ids
      .map(id => lookup.get(id) ?? fallback)
      .join(', ');
  }

  protected selectedMemberLabels(): { id: string; label: string }[] {
    const ids = this.groupForm.controls.memberUserIds.value ?? [];
    if (!ids.length) {
      return [];
    }
    const lookup = this.userNameMap();
    const fallback = this.transloco.translate('Settings.ShareGroups.MemberFallback');
    return ids.map(id => ({ id, label: lookup.get(id) ?? fallback }));
  }

  protected toggleMember(userId: string | null | undefined, checked: boolean): void {
    if (!userId) {
      return;
    }
    const control = this.groupForm.controls.memberUserIds;
    const current = new Set(control.value ?? []);
    if (checked) {
      current.add(userId);
    } else {
      current.delete(userId);
    }
    control.setValue(Array.from(current));
    control.markAsDirty();
    control.markAsTouched();
  }

  protected isMemberSelected(userId: string | null | undefined): boolean {
    if (!userId) {
      return false;
    }
    const control = this.groupForm.controls.memberUserIds;
    return (control.value ?? []).includes(userId);
  }

  protected isDeleting(group: ShareGroupDto): boolean {
    const id = group.id ?? null;
    if (!id) {
      return false;
    }
    return this.deletingGroupId() === id;
  }

  protected async saveGroup(): Promise<void> {
    this.groupForm.markAllAsTouched();
    if (this.groupForm.invalid) {
      this.toast.error(this.transloco.translate('Settings.ShareGroups.Form.ValidationError'));
      return;
    }

    const raw = this.groupForm.getRawValue();
    const trimmedName = raw.name.trim();
    if (!trimmedName) {
      this.groupForm.controls.name.setValue('');
      this.toast.error(this.transloco.translate('Settings.ShareGroups.Form.ValidationError'));
      return;
    }

    const payloadIds = this.toIds(raw.memberUserIds);
    const editingId = this.editingGroupId();

    this.savingGroup.set(true);
    try {
      if (editingId) {
        const request: UpdateShareGroupRequest = {
          name: trimmedName,
          memberUserIds: payloadIds,
        };
        const response = await firstValueFrom(
          this.shareGroupsService.apiShareGroupsGroupIdPut$Json({ groupId: editingId, body: request })
        );
        if (response.success === false && response.message) {
          this.toast.error(response.message);
          return;
        }
        const updated = response.data ?? { id: editingId, name: trimmedName, memberUserIds: payloadIds };
        this.updateGroupList(updated);
        const successMsg = response.message ?? this.transloco.translate('Settings.ShareGroups.Toast.Saved');
        this.toast.success(successMsg);
        this.groupForm.markAsPristine();
      } else {
        const request: CreateShareGroupRequest = {
          name: trimmedName,
          memberUserIds: payloadIds,
        };
        const response = await firstValueFrom(this.shareGroupsService.apiShareGroupsPost$Json({ body: request }));
        if (response.success === false && response.message) {
          this.toast.error(response.message);
          return;
        }
        const created = response.data ?? { id: undefined, name: trimmedName, memberUserIds: payloadIds };
        this.groups.update(current => [...current, this.normalizeGroup(created)]);
        const successMsg = response.message ?? this.transloco.translate('Settings.ShareGroups.Toast.Saved');
        this.toast.success(successMsg);
        if (created.id) {
          this.editGroup(created);
        } else {
          this.groupForm.markAsPristine();
        }
      }
    } catch (error) {
      this.toast.error(this.toErrorMessage(error, this.transloco.translate('Settings.ShareGroups.Toast.SaveError')));
    } finally {
      this.savingGroup.set(false);
    }
  }

  protected async deleteGroup(group: ShareGroupDto): Promise<void> {
    const id = group.id ?? null;
    if (!id) {
      return;
    }
    const confirmMessage = this.transloco.translate('Settings.ShareGroups.Delete.Confirm', {
      name: group.name ?? this.transloco.translate('Settings.ShareGroups.Untitled'),
    });
    if (!window.confirm(confirmMessage)) {
      return;
    }

    this.deletingGroupId.set(id);
    try {
      await firstValueFrom(this.shareGroupsService.apiShareGroupsGroupIdDelete({ groupId: id }));
      this.groups.update(current => current.filter(g => g.id !== id));
      if (this.editingGroupId() === id) {
        this.startCreate();
      }
      this.toast.success(this.transloco.translate('Settings.ShareGroups.Toast.Deleted'));
    } catch (error) {
      this.toast.error(this.toErrorMessage(error, this.transloco.translate('Settings.ShareGroups.Toast.DeleteError')));
    } finally {
      this.deletingGroupId.set(null);
    }
  }

  protected trackByGroupId(_: number, group: ShareGroupDto): string {
    return group.id ?? group.name ?? '';
  }

  protected trackByUserId(_: number, user: UserDto): string {
    return user.id ?? user.name ?? '';
  }

  private async loadGroups(): Promise<void> {
    this.loadingGroups.set(true);
    try {
      const response = await firstValueFrom(this.shareGroupsService.apiShareGroupsGet$Json());
      if (response.success === false && response.message) {
        this.toast.error(response.message);
        return;
      }
      const groups = response.data ?? [];
      this.groups.set(groups.map(group => this.normalizeGroup(group)));
    } catch (error) {
      this.toast.error(this.toErrorMessage(error, this.transloco.translate('Settings.ShareGroups.Toast.LoadError')));
    } finally {
      this.loadingGroups.set(false);
    }
  }

  private async loadUsers(): Promise<void> {
    this.loadingUsers.set(true);
    try {
      const response = await firstValueFrom(this.usersService.apiUsersOthersGet$Json());
      if (response.success === false && response.message) {
        this.toast.error(response.message);
        return;
      }
      const users = (response.data ?? []).filter(user => (user.id ?? '').length > 0);
      users.sort((a, b) => this.compareNames(a.name, b.name));
      this.users.set(users);
      const lookup = new Map<string, string>();
      for (const user of users) {
        if (user.id) {
          lookup.set(user.id, (user.name ?? '').trim() || user.id);
        }
      }
      this.userNameMap.set(lookup);
    } catch (error) {
      this.toast.error(this.toErrorMessage(error, this.transloco.translate('Settings.ShareGroups.Toast.UsersLoadError')));
    } finally {
      this.loadingUsers.set(false);
    }
  }

  private updateGroupList(updated: ShareGroupDto): void {
    this.groups.update(current => {
      const normalized = this.normalizeGroup(updated);
      let replaced = false;
      const list = current.map(group => {
        if (group.id === normalized.id) {
          replaced = true;
          return { ...group, ...normalized };
        }
        return group;
      });
      if (!replaced) {
        list.push(normalized);
      }
      return list;
    });
  }

  private compareNames(a?: string | null, b?: string | null): number {
    const left = (a ?? '').trim().toLocaleLowerCase();
    const right = (b ?? '').trim().toLocaleLowerCase();
    return left.localeCompare(right, undefined, { sensitivity: 'base' });
  }

  private toIds(ids?: readonly string[] | null): string[] {
    return (ids ?? []).filter(id => typeof id === 'string' && id.length > 0);
  }

  private normalizeGroup(group: ShareGroupDto): ShareGroupDto {
    return {
      ...group,
      memberUserIds: this.toIds(group.memberUserIds),
    };
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
